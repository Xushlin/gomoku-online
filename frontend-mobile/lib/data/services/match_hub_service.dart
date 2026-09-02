/// The realtime connection.
///
/// `signalr_netcore` was proven able to talk to this hub before any of this UI
/// existed — see `test/hub_probe_test.dart`. That ordering was the point: an
/// unusable transport is a bigger decision than the screens.
library;

import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:signalr_netcore/hub_connection.dart';
import 'package:signalr_netcore/hub_connection_builder.dart';

/// One room's live state, as the hub reports it.
class RoomSnapshot {
  RoomSnapshot(this.raw);

  final Map<String, dynamic> raw;

  String get status => '${raw['status']}';
  Map<String, dynamic>? get game => raw['game'] as Map<String, dynamic>?;
  List<dynamic> get moves => (game?['moves'] as List<dynamic>?) ?? const [];
  List<dynamic> get seats => (raw['seats'] as List<dynamic>?) ?? const [];

  /// Whose turn it is, as a seat index. Null before the game starts.
  int? get currentSeat => game?['currentSeat'] as int?;
}

class MatchHub {
  MatchHub({required this.serverAddress, required this.accessToken});

  final String serverAddress;

  /// Read lazily: the token is refreshed while the app runs, and a captured string
  /// would go stale mid-session.
  final String Function() accessToken;

  HubConnection? _connection;

  final _state = ValueNotifier<RoomSnapshot?>(null);
  final _errors = StreamController<String>.broadcast();

  ValueListenable<RoomSnapshot?> get state => _state;
  Stream<String> get errors => _errors.stream;

  bool get connected => _connection?.state == HubConnectionState.Connected;

  Future<void> connect() async {
    if (_connection != null) return;

    // The token goes on the query string — the hub reads it there. This is the part
    // most likely to be unsupported by a third-party client, which is exactly why
    // the probe tested it first.
    final connection = HubConnectionBuilder()
        .withUrl('$serverAddress/hubs/match?access_token=${Uri.encodeComponent(accessToken())}')
        .withAutomaticReconnect(retryDelays: [0, 2000, 5000, 10000, 30000])
        .build();

    // **`RoomState`, and the name is the whole bug this once was.**
    //
    // This read `RoomStateChanged` — a method the server has never sent. SignalR
    // silently ignores a subscription to a name nobody invokes, so the entire *inbound*
    // half of this connection was dead from the first day and nothing said so:
    // outbound worked, every test asserted the **server's** state over REST, and the
    // board people saw was the one-shot REST snapshot from `RoomRepository.open`.
    //
    // Found by looking at a real device: a second player joined, the server said
    // `Playing`, and the screen still said 等待中. `test/hub_contract_test.dart` now
    // derives the valid names from the server's own source.
    connection.on('RoomState', (args) {
      if (args != null && args.isNotEmpty && args.first is Map) {
        _state.value = RoomSnapshot(Map<String, dynamic>.from(args.first as Map));
      }
    });
    connection.on('GameEnded', (args) {
      if (args != null && args.isNotEmpty) _errors.add('game-ended');
    });

    await connection.start();
    _connection = connection;
  }

  Future<void> joinRoom(String roomId) async {
    await connect();
    await _connection!.invoke('JoinRoom', args: [roomId]);
  }

  /// Places a stone. **The server judges legality, not this client.**
  ///
  /// Same rule as the web client's xiangqi board (design D2): a second copy of the
  /// rules is a second truth, and when the two disagree the player reads it as
  /// "that move was clearly legal".
  Future<void> makeMove(String roomId, int row, int col) async {
    await _connection!.invoke('MakeMove', args: [roomId, row, col]);
  }

  /// A relocation: `from → to`. **A separate method, not extra arguments on
  /// [makeMove].**
  ///
  /// SignalR applies no C# optional-parameter defaults in either direction: a client
  /// sending fewer *or more* arguments than the hub method declares is rejected in the
  /// binding layer — before any filter, and below the configured log level, so it is
  /// invisible from both ends. Adding a parameter to a live hub method is a breaking
  /// change; adding a method is not.
  Future<void> movePiece(
    String roomId,
    int fromRow,
    int fromCol,
    int row,
    int col,
  ) async {
    await _connection!.invoke(
      'MovePiece',
      args: [roomId, fromRow, fromCol, row, col],
    );
  }

  Future<void> dispose() async {
    await _connection?.stop();
    _connection = null;
    await _errors.close();
    _state.dispose();
  }
}
