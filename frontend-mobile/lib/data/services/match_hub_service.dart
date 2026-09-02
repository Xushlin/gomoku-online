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
  final _dissolved = ValueNotifier<int>(0);
  final _urged = ValueNotifier<int>(0);
  final _chat = ValueNotifier<Map<String, dynamic>?>(null);
  Map<String, dynamic>? _lastUrge;

  ValueListenable<RoomSnapshot?> get state => _state;

  /// The most recent chat message the server pushed. **One message, not a list.**
  ValueListenable<Map<String, dynamic>?> get chat => _chat;

  /// Bumped each time somebody urges this user. See the subscription for why it is a
  /// counter and not a flag.
  ValueListenable<int> get urged => _urged;

  /// The most recent `UrgeDto` — who urged, and when.
  Map<String, dynamic>? get lastUrge => _lastUrge;

  /// Bumped each time the server says a room was dissolved.
  ///
  /// A counter rather than a bool, because "it happened again" has to be observable:
  /// a bool that is already true reports nothing the second time.
  ValueListenable<int> get dissolved => _dissolved;

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
    // **After this there is no `RoomState`** — the room is physically deleted. So
    // ignoring this push is not "one missing toast": it leaves a person sitting on the
    // board of a room that no longer exists, where every tap is an error.
    connection.on('RoomDissolved', (args) {
      _dissolved.value = _dissolved.value + 1;
    });

    // **Being urged is a push, not part of any snapshot.** The server never writes
    // "you have been urged" into `RoomStateDto`, so an implementation that re-fetches
    // the room to find out would never find out.
    //
    // A counter beside the payload for the same reason `dissolved` is a counter: "it
    // happened again" has to be observable, and a value that is already set reports
    // nothing the second time.
    // **One message per push, not the whole conversation.** A listener that treated
    // this as the new list would wipe the history on the first thing anybody said.
    connection.on('ChatMessage', (args) {
      if (args != null && args.isNotEmpty && args.first is Map) {
        _chat.value = Map<String, dynamic>.from(args.first! as Map);
      }
    });

    connection.on('UrgeReceived', (args) {
      if (args != null && args.isNotEmpty && args.first is Map) {
        _lastUrge = Map<String, dynamic>.from(args.first! as Map);
      }
      _urged.value = _urged.value + 1;
    });

    await connection.start();
    _connection = connection;
  }

  Future<void> joinRoom(String roomId) async {
    await connect();
    await _connection!.invoke('JoinRoom', args: [roomId]);
  }

  /// Leaves the room's broadcast group.
  ///
  /// **Not optional cleanup.** Staying in the group means this client keeps receiving
  /// that room's pushes after it has moved on: enter A, leave, enter B, and a move in A
  /// repaints B's board with A's position. That was harmless for exactly as long as the
  /// inbound half was dead — see `fix-mobile-hub-inbound`. Fixing inbound made it live.
  Future<void> leaveRoom(String roomId) async {
    final connection = _connection;
    if (connection == null) return;
    await connection.invoke('LeaveRoom', args: [roomId]);
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

  /// Urges whoever owes a move.
  ///
  /// **One argument, and that is the whole signature.** SignalR applies no C#
  /// optional-parameter defaults in either direction, so sending more or fewer is
  /// rejected in the binding layer — invisibly, from both ends.
  ///
  /// The server decides who gets urged (`Room.UrgeOpponent` urges **the player who
  /// owes a move**, not "the other seat"), enforces the 30-second cooldown, and
  /// refuses when it is the caller's own turn. None of that is re-implemented here.
  Future<void> urge(String roomId) async {
    await _connection!.invoke('Urge', args: [roomId]);
  }

  /// Joins the spectator sub-group.
  ///
  /// **Idempotent, and a silent no-op for anybody who is not a spectator** — the server
  /// asks the aggregate rather than believing the caller, so this needs no "am I a
  /// spectator" guard on this side. A guard here would be a second judgement that can
  /// go stale; the server's cannot.
  Future<void> joinSpectatorGroup(String roomId) async {
    await _connection!.invoke('JoinSpectatorGroup', args: [roomId]);
  }

  /// Says something in a room.
  ///
  /// **The channel goes as a string, and that was measured rather than inferred.** Both
  /// the REST pipeline and the hub register `JsonStringEnumConverter`, and
  /// `test/room_social_probe_test.dart` confirmed `'Room'` binds against the live hub
  /// (an integer binds too; the string form matches how this client reads every other
  /// enum). Reading the DI registration would not have been enough: SignalR rejects a
  /// badly-typed argument in the binding layer, before any filter and below the log
  /// level, invisibly from both ends.
  ///
  /// **Three arguments, exactly.** No optional-parameter defaults are applied in either
  /// direction.
  Future<void> sendChat(String roomId, String content, ChatChannelWire channel) async {
    await _connection!.invoke('SendChat', args: [roomId, content, channel.wire]);
  }

  Future<void> dispose() async {
    await _connection?.stop();
    _connection = null;
    _state.dispose();
    _dissolved.dispose();
    _urged.dispose();
    _chat.dispose();
  }
}

/// The wire name of a chat channel.
///
/// A separate two-value type rather than importing the model's `ChatChannel`: this file
/// is a **service**, and services here do not know about models — the repository is
/// where JSON becomes a model. Two values is small enough that the duplication cannot
/// drift unnoticed, and `chat_test.dart` asserts the two agree.
enum ChatChannelWire {
  room('Room'),
  spectator('Spectator');

  const ChatChannelWire(this.wire);

  final String wire;
}
