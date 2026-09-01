/// Rooms and live play. **The only place room JSON becomes a model.**
library;

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../models/models.dart';
import '../services/match_hub_service.dart';

class RoomFailure implements Exception {
  const RoomFailure(this.code, [this.status]);

  final String code;
  final int? status;

  @override
  String toString() => 'RoomFailure($code, $status)';
}

class RoomRepository {
  RoomRepository({required this._dio, required this._hub});

  final Dio _dio;
  final MatchHub _hub;

  /// The live room, pushed by the hub. A [ValueListenable] rather than a Stream so a
  /// late listener sees the current value instead of waiting for the next change.
  ValueListenable<Room?> get live => _live;
  final _live = ValueNotifier<Room?>(null);

  Future<List<Room>> list(String gameKey) async {
    final response = await _dio.get<dynamic>(
      '/api/rooms',
      queryParameters: {'gameKey': gameKey},
    );
    _refuseFailure(response);
    return [
      for (final r in (response.data as List<dynamic>? ?? const []))
        Room.fromJson(r as Map<String, dynamic>),
    ];
  }

  Future<Room> create(String name, String gameKey) async {
    final response = await _dio.post<dynamic>(
      '/api/rooms',
      data: {'name': name, 'gameKey': gameKey},
    );
    _refuseFailure(response);
    return Room.fromJson(response.data as Map<String, dynamic>);
  }

  /// Joining a room you already sit in answers 409, and that is not a failure worth
  /// blocking on — the server owns seats, and the caller's intent (open this room)
  /// is still satisfiable.
  Future<void> join(String roomId) async {
    final response = await _dio.post<dynamic>('/api/rooms/$roomId/join');
    if (response.statusCode == 409) return;
    _refuseFailure(response);
  }

  Future<Room> byId(String roomId) async {
    final response = await _dio.get<dynamic>('/api/rooms/$roomId');
    _refuseFailure(response);
    return Room.fromJson(response.data as Map<String, dynamic>);
  }

  /// Opens the live connection and starts publishing to [live].
  ///
  /// REST first: the snapshot is the authoritative recovery source and the hub only
  /// pushes *changes*, so relying on the hub alone leaves an empty board until
  /// somebody moves.
  Future<Room> open(String roomId) async {
    final snapshot = await byId(roomId);
    _live.value = snapshot;

    _hub.state.addListener(_republish);
    await _hub.joinRoom(roomId);
    return snapshot;
  }

  void _republish() {
    final pushed = _hub.state.value;
    if (pushed != null) _live.value = Room.fromJson(pushed.raw);
  }

  /// Sends a move. **The server judges legality, not this client** (design D2).
  Future<void> makeMove(String roomId, int row, int col) =>
      _hub.makeMove(roomId, row, col);

  Future<void> close() async {
    _hub.state.removeListener(_republish);
    await _hub.dispose();
    _live.dispose();
  }

  void _refuseFailure(Response<dynamic> response) {
    final status = response.statusCode ?? 0;
    if (status < 400) return;
    final body = response.data;
    final code = body is Map
        ? '${body['code'] ?? body['title'] ?? body['detail'] ?? ''}'
        : '';
    throw RoomFailure(code.isEmpty ? 'generic' : code, status);
  }
}
