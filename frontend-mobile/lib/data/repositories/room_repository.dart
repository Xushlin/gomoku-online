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

  /// Bumped when the room this repository has open was dissolved by its host.
  ///
  /// **There is no `RoomState` after a dissolve** — the room is deleted — so a screen
  /// that waits for one waits forever.
  ValueListenable<int> get dissolved => _dissolved;
  final _dissolved = ValueNotifier<int>(0);

  /// Which room `open` last opened. Used to ignore pushes for rooms we have left.
  String? _openRoomId;
  bool _listening = false;

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

  /// Creates a room against the machine.
  ///
  /// **`POST /api/rooms/ai`, checked against the controller's own attribute before it
  /// was written** — the previous change guessed a route (`POST .../dissolve`) that does
  /// not exist, and the unit test beside it asserted the guess and passed.
  /// `test/room_route_contract_test.dart` covers this one too.
  ///
  /// `difficulty` is `Easy` / `Medium` / `Hard` and `humanSide` is `Black` / `White`;
  /// both are the server's spellings. A wrong `difficulty` comes back as a **binding**
  /// error (`"The body field is required"` plus a JSON conversion failure on
  /// `$.difficulty`), not a domain one, so there is no field-level message to show.
  Future<Room> createAiRoom({
    required String name,
    required String gameKey,
    required String difficulty,
    required String humanSide,
  }) async {
    final response = await _dio.post<dynamic>(
      '/api/rooms/ai',
      data: {
        'name': name,
        'gameKey': gameKey,
        'difficulty': difficulty,
        'humanSide': humanSide,
      },
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
    _openRoomId = roomId;

    // **Registered once, not once per room.** This used to `addListener` on every
    // `open` with nothing ever removing it, so five rooms meant five registrations and
    // five parses per push. Harmless, because `_republish` is idempotent — and harmless
    // is exactly how a thing like this survives.
    if (!_listening) {
      _hub.state.addListener(_republish);
      _hub.dissolved.addListener(_onDissolved);
      _listening = true;
    }
    await _hub.joinRoom(roomId);
    return snapshot;
  }

  /// Leaves the room, on the server and on the hub.
  ///
  /// **Which route is the server's rule, not this client's preference:** the host of a
  /// *waiting* room is refused by `/leave` (`HostCannotLeaveWaitingRoom`) and must use
  /// `/dissolve`, which in turn only exists for waiting rooms. Writing it as a client
  /// choice would invite the next reader to try both.
  /// **Dissolve is `DELETE /api/rooms/{id}`, and getting that wrong is measured
  /// history, not caution.** The first version of this method posted to
  /// `/api/rooms/{id}/dissolve` — a route that does not exist — and the unit test
  /// beside it asserted that exact path and passed, because a fake adapter accepts any
  /// URL. **A test asserting that the client sent the URL the client was written to
  /// send knows nothing about the URL the server has.** Only the live server said 404.
  /// `test/room_route_contract_test.dart` now derives the legal routes from the
  /// controller's own attributes.
  Future<void> leave(String roomId, {required bool asHostOfWaitingRoom}) async {
    final response = asHostOfWaitingRoom
        ? await _dio.delete<dynamic>('/api/rooms/$roomId')
        : await _dio.post<dynamic>('/api/rooms/$roomId/leave');
    // 404 means it is already gone, which is the outcome the caller wanted.
    if (response.statusCode != 404) _refuseFailure(response);

    await _hub.leaveRoom(roomId);
    if (_openRoomId == roomId) _openRoomId = null;
  }

  void _republish() {
    final pushed = _hub.state.value;
    if (pushed == null) return;
    final room = Room.fromJson(pushed.raw);

    // **A second guard, and it is not redundant with leaving the group.** Leaving is
    // asynchronous and a push already in flight can land after it; and a push for a
    // room we are not looking at must never repaint the one we are. Enter A, leave,
    // enter B, someone moves in A — without this, B's board shows A.
    final open = _openRoomId;
    if (open != null && room.id != open) return;
    _live.value = room;
  }

  void _onDissolved() => _dissolved.value = _dissolved.value + 1;

  /// Gives up this game. **Irreversible — the View asks first.**
  ///
  /// The server names the winner and pushes `GameEnded`; this method returns nothing on
  /// purpose. A caller that wrote down "I lost" from the fact that this succeeded would
  /// be a **second** path announcing an outcome, and the way two such paths fail is one
  /// of them naming the wrong winner.
  Future<void> resign(String roomId) async {
    final response = await _dio.post<dynamic>('/api/rooms/$roomId/resign');
    _refuseFailure(response);
  }

  /// Urges whoever owes a move.
  ///
  /// Every rule about *whether* this is allowed — playing, a player, not your own turn,
  /// 30-second cooldown — lives on the server. This client does not keep a timer.
  Future<void> urge(String roomId) => _hub.urge(roomId);

  /// Bumped each time somebody urges this user, with the payload beside it.
  ValueListenable<int> get urged => _hub.urged;

  /// Who urged, last. Null until the first one arrives.
  String? get lastUrgedBy => _hub.lastUrge?['fromUsername'] as String?;

  /// Sends a move. **The server judges legality, not this client** (design D2).
  Future<void> makeMove(String roomId, int row, int col) =>
      _hub.makeMove(roomId, row, col);

  /// Sends a relocation. **The server judges legality, not this client** (design D2).
  Future<void> movePiece(String roomId, int fromRow, int fromCol, int row, int col) =>
      _hub.movePiece(roomId, fromRow, fromCol, row, col);

  /// Tears the connection down for good — app shutdown, not room exit.
  ///
  /// **Room exit goes through [leave], which keeps the connection.** Stopping the hub
  /// on every exit would mean a fresh handshake for every room, and `_live` must stay
  /// alive because the next room reuses it.
  Future<void> close() async {
    if (_listening) {
      _hub.state.removeListener(_republish);
      _hub.dissolved.removeListener(_onDissolved);
      _listening = false;
    }
    _openRoomId = null;
    await _hub.dispose();
    _live.dispose();
    _dissolved.dispose();
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
