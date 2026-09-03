// Watching a game you are not playing.
//
// **The three-step entry is the whole subtlety.** `POST /spectate` → `JoinRoom` →
// `JoinSpectatorGroup`, and the middle one is the one that gets skipped: room-channel
// chat and room state go to the *room* group, which `JoinRoom` joins;
// `JoinSpectatorGroup` only adds the spectator sub-group.
//
// This is not a guess. `test/room_social_probe_test.dart` made exactly that mistake and
// measured "a spectator cannot hear the table" — which reads like a server bug and is
// not one.
import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:gewu_mobile/data/models/models.dart';
import 'package:gewu_mobile/data/repositories/auth_repository.dart';
import 'package:gewu_mobile/data/repositories/game_catalog_repository.dart';
import 'package:gewu_mobile/data/repositories/room_repository.dart';
import 'package:gewu_mobile/data/services/dio_client.dart';
import 'package:gewu_mobile/data/services/match_hub_service.dart';
import 'package:gewu_mobile/data/services/token_store.dart';
import 'package:gewu_mobile/ui/game/view_model/game_view_model.dart';
import 'package:gewu_mobile/data/repositories/settings_repository.dart';
import 'package:gewu_mobile/data/repositories/sound_repository.dart';
import 'package:gewu_mobile/data/services/preferences_store.dart';
import 'package:gewu_mobile/data/services/sound_player.dart';
import 'package:gewu_mobile/ui/lobby/view_model/lobby_view_model.dart';

const _me = 'me-1';
const _p0 = 'p0';
const _p1 = 'p1';

const _json = {
  Headers.contentTypeHeader: [Headers.jsonContentType],
};

const _games = '[{"gameKey":"gomoku","isRated":true,"supportsHumanVsHuman":true,'
    '"supportsAi":true,"seatCount":2,"rows":15,"cols":15}]';

String roomJson({
  String status = 'Playing',
  List<String?> seatedBy = const [_p0, _p1],
  List<String> spectators = const [],
  int seatCount = 2,
}) => jsonEncode({
  'id': 'r1',
  'name': 'room',
  'gameKey': 'gomoku',
  'status': status,
  'seatCount': seatCount,
  'seats': [
    for (var i = 0; i < seatedBy.length; i++)
      {
        'index': i,
        if (seatedBy[i] != null) 'player': {'id': seatedBy[i], 'username': 'seat$i'},
      },
  ],
  'host': {'id': _p0, 'username': 'host'},
  'game': {'moves': <dynamic>[], 'currentSeat': 0},
  'spectators': [
    for (final s in spectators) {'id': s, 'username': 'w-$s'},
  ],
});

/// What `GET /api/rooms` actually returns for one room.
///
/// **Measured, and it is not the same shape as `GET /api/rooms/{id}`.** The list serves
/// `RoomSummaryDto`, which carries **no `seatCount`** and lists **only the taken
/// seats** — so `seats.length` is how many players are present, not how many seats
/// exist.
///
/// The first version of these lobby tests built a full room instead, with `seatCount`
/// in it. Every assertion was green, and the lobby shipped offering "watch" on every
/// room including the empty ones: **a fixture the screen never receives proves nothing
/// about the screen.** An integration test caught it.
String summaryJson({String status = 'Playing', int taken = 2}) => jsonEncode({
  'id': 'r1',
  'name': 'room',
  'gameKey': 'gomoku',
  'status': status,
  'host': {'id': _p0, 'username': 'host'},
  'seats': [
    for (var i = 0; i < taken; i++)
      {'index': i, 'player': {'id': 'seat$i', 'username': 'seat$i'}},
  ],
  'spectatorCount': 0,
});

/// Records every call, verb and path, in order.
class SpectateAdapter implements HttpClientAdapter {
  SpectateAdapter(this.room);

  String room;
  final calls = <String>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    if (options.path.startsWith('/api/games')) {
      return ResponseBody.fromString(_games, 200, headers: _json);
    }
    calls.add('${options.method} ${options.path}');
    if (options.method == 'GET') {
      return ResponseBody.fromString(room, 200, headers: _json);
    }
    return ResponseBody.fromString('{}', 200, headers: _json);
  }

  @override
  void close({bool force = false}) {}
}

/// Records hub calls in the same list as the HTTP ones, so **order** is observable.
class OrderedHub extends MatchHub {
  OrderedHub(this.calls)
      : super(serverAddress: 'http://example.invalid', accessToken: _empty);

  static String _empty() => '';

  final List<String> calls;
  final pushes = ValueNotifier<RoomSnapshot?>(null);
  final dissolves = ValueNotifier<int>(0);
  final urges = ValueNotifier<int>(0);
  final incoming = ValueNotifier<Map<String, dynamic>?>(null);
  final moves = <String>[];

  @override
  ValueListenable<RoomSnapshot?> get state => pushes;

  @override
  ValueListenable<int> get dissolved => dissolves;

  @override
  ValueListenable<int> get urged => urges;

  @override
  ValueListenable<Map<String, dynamic>?> get chat => incoming;

  @override
  Future<void> joinRoom(String roomId) async => calls.add('HUB JoinRoom');

  @override
  Future<void> leaveRoom(String roomId) async => calls.add('HUB LeaveRoom');

  @override
  Future<void> joinSpectatorGroup(String roomId) async =>
      calls.add('HUB JoinSpectatorGroup');

  @override
  Future<void> makeMove(String roomId, int row, int col) async =>
      moves.add('$row,$col');

  @override
  Future<void> movePiece(String roomId, int a, int b, int c, int d) async =>
      moves.add('$a,$b->$c,$d');

  @override
  Future<void> sendChat(String roomId, String content, ChatChannelWire channel) async =>
      calls.add('HUB SendChat ${channel.wire}');
}

Future<({RoomRepository rooms, SpectateAdapter adapter, OrderedHub hub, Dio dio})>
    build(String room) async {
  final adapter = SpectateAdapter(room);
  final hub = OrderedHub(adapter.calls);
  final dio = buildDio(
    baseUrl: 'http://example.invalid',
    tokens: MemoryTokenStore(),
    refresh: () async => false,
    adapter: adapter,
  );
  return (rooms: RoomRepository(dio: dio, hub: hub), adapter: adapter, hub: hub, dio: dio);
}

Future<GameViewModel> viewModelFor(
  ({RoomRepository rooms, SpectateAdapter adapter, OrderedHub hub, Dio dio}) parts, {
  String myId = _me,
}) async {
  final catalog = GameCatalogRepository(parts.dio);
  await catalog.load();
  final auth = AuthRepository(dio: parts.dio, tokens: MemoryTokenStore())
    ..currentUser = AuthUser(id: myId, username: 'me');
  final vm = GameViewModel(
    rooms: parts.rooms,
    catalog: catalog,
    auth: auth,
    sound: recordingSound(),
    roomId: 'r1',
  );
  await vm.open();
  return vm;
}

/// A sound repository over a fake device, so a test can assert what was played — and,
/// just as importantly, what was not.
SoundRepository recordingSound([RecordingSoundPlayer? player]) => SoundRepository(
  player: player ?? RecordingSoundPlayer(),
  settings: SettingsRepository(MemoryPreferencesStore()),
);

void main() {
  group('entering as a spectator', () {
    test('three steps, in order', () async {
      // **Order, not just presence.** A test that only asserted "all three were called"
      // would pass an implementation that joins the spectator sub-group before the room
      // group — and the failure mode this guards against is subtler than that: skipping
      // `JoinRoom` entirely leaves a spectator who cannot hear the table.
      //
      // Positive control: delete the `open(roomId)` call (which is what calls
      // `JoinRoom`) and this goes red.
      final parts = await build(roomJson(spectators: const [_me]));
      await parts.rooms.spectate('r1');

      final steps = parts.adapter.calls
          .where((c) => c.contains('spectate') || c.startsWith('HUB'))
          .toList();
      expect(steps, [
        'POST /api/rooms/r1/spectate',
        'HUB JoinRoom',
        'HUB JoinSpectatorGroup',
      ]);
    });

    test('and the room comes back, so the board can be drawn', () async {
      final parts = await build(roomJson(spectators: const [_me]));
      final room = await parts.rooms.spectate('r1');
      expect(room.id, 'r1');
      expect(room.gameKey, 'gomoku', reason: 'the board reads its shape from this');
    });

    test('the spectator list is parsed', () async {
      final parts = await build(roomJson(spectators: const [_me, 'w2']));
      final vm = await viewModelFor(parts);
      expect(vm.room?.spectators.map((s) => s.playerId), [_me, 'w2']);
      expect(vm.isSpectator, isTrue);
    });

    test('a player in the same room is not a spectator', () async {
      // The other direction: without it, `isSpectator` returning true for everybody
      // passes the test above and silently makes every board read-only.
      final parts = await build(roomJson(spectators: const ['w2']));
      final vm = await viewModelFor(parts, myId: _p0);
      expect(vm.mySeat, 0, reason: 'precondition — we are playing');
      expect(vm.isSpectator, isFalse);
    });
  });

  group('a spectator sends no moves', () {
    test('tapping the board does nothing', () async {
      final parts = await build(roomJson(spectators: const [_me]));
      final vm = await viewModelFor(parts);
      expect(vm.isSpectator, isTrue, reason: 'precondition');

      await vm.tap(3, 3);
      expect(parts.hub.moves, isEmpty);
    });

    test('but a player tapping the same square does send', () async {
      // **The precondition for the test above.** "Nothing was sent" is green when the
      // whole path is broken, so the same tap must demonstrably work for a player.
      //
      // Positive control: drop `isSpectator` from the guard in `tap` and the previous
      // test goes red while this one stays green.
      final parts = await build(roomJson(spectators: const []));
      final vm = await viewModelFor(parts, myId: _p0);
      expect(vm.isSpectator, isFalse, reason: 'precondition');

      await vm.tap(3, 3);
      expect(parts.hub.moves, ['3,3']);
    });

    test('and sees no resign or urge entry', () async {
      final parts = await build(roomJson(spectators: const [_me]));
      final vm = await viewModelFor(parts);
      expect(vm.canResign, isFalse);
      expect(vm.canUrge, isFalse);
    });
  });

  group('leaving takes the route that matches who you are', () {
    test('a spectator deletes their spectate', () async {
      final parts = await build(roomJson(spectators: const [_me]));
      final vm = await viewModelFor(parts);
      expect(vm.isSpectator, isTrue, reason: 'precondition');

      await vm.leave();
      expect(parts.adapter.calls, contains('DELETE /api/rooms/r1/spectate'));
      expect(
        parts.adapter.calls.where((c) => c.contains('/leave')),
        isEmpty,
        reason: 'that is the player route',
      );
    });

    test('a player still posts leave', () async {
      final parts = await build(roomJson(spectators: const []));
      final vm = await viewModelFor(parts, myId: _p0);
      expect(vm.isSpectator, isFalse, reason: 'precondition');

      await vm.leave();
      expect(parts.adapter.calls, contains('POST /api/rooms/r1/leave'));
      expect(parts.adapter.calls.where((c) => c.contains('spectate')), isEmpty);
    });

    test('a spectator is not asked to confirm', () async {
      // The warning is about a seat whose clock keeps running. A spectator has neither.
      final watching = await build(roomJson(spectators: const [_me]));
      expect((await viewModelFor(watching)).leavingNeedsConfirmation, isFalse);

      final playing = await build(roomJson(spectators: const []));
      expect(
        (await viewModelFor(playing, myId: _p0)).leavingNeedsConfirmation,
        isTrue,
        reason: 'and a player still is — or the line above is green for the wrong reason',
      );
    });
  });

  group('the chat channels somebody can reach', () {
    test('a player gets one, a spectator gets two', () async {
      // **The criterion is who can reach the channel, not whether this client supports
      // spectating.** Written the second way, the spectator tab appears in front of
      // players the day spectating ships — a permanently empty tab, which looks broken.
      //
      // Positive control: return both channels unconditionally and the player half of
      // this goes red.
      final playing = await build(roomJson(spectators: const []));
      final player = await viewModelFor(playing, myId: _p0);
      expect(player.chatChannels, [ChatChannel.room]);

      final watching = await build(roomJson(spectators: const [_me]));
      final spectator = await viewModelFor(watching);
      expect(spectator.chatChannels, [ChatChannel.room, ChatChannel.spectator]);
    });

    test('a player cannot select the spectator channel', () async {
      final parts = await build(roomJson(spectators: const []));
      final vm = await viewModelFor(parts, myId: _p0);
      vm.chooseChatChannel(ChatChannel.spectator);
      expect(vm.chatChannel, ChatChannel.room, reason: 'a channel they cannot reach');
    });

    test('a spectator sends on the channel they chose', () async {
      final parts = await build(roomJson(spectators: const [_me]));
      final vm = await viewModelFor(parts);
      vm.chooseChatChannel(ChatChannel.spectator);

      await vm.sendChat('to the other watchers');
      expect(parts.adapter.calls, contains('HUB SendChat Spectator'));
    });
  });

  group('the lobby decides by free seat, not by status', () {
    Future<LobbyViewModel> lobbyFor(
      ({RoomRepository rooms, SpectateAdapter adapter, OrderedHub hub, Dio dio}) parts,
    ) async {
      final catalog = GameCatalogRepository(parts.dio);
      await catalog.load();
      return LobbyViewModel(
        rooms: parts.rooms,
        auth: AuthRepository(dio: parts.dio, tokens: MemoryTokenStore()),
        catalog: catalog,
        gameKey: 'gomoku',
      );
    }

    test('a half-empty room offers a seat, on the shape the lobby really gets', () async {
      // **The fixture is a summary, not a room.** One taken seat, no `seatCount`. The
      // earlier version of this test built a full room and was green while the lobby
      // offered "watch" on every room in it.
      //
      // Positive control: read the total off the room (`takenSeats < totalSeats`) and
      // this goes red — `1 < 1` is false.
      final parts = await build(roomJson(seatedBy: const [_p0, null], status: 'Waiting'));
      final lobby = await lobbyFor(parts);

      final half = Room.fromJson(jsonDecode(summaryJson(status: 'Waiting', taken: 1)) as Map<String, dynamic>);
      expect(half.takenSeats, 1, reason: 'precondition — one player present');
      expect(half.seatCount, isNull, reason: 'precondition — a summary carries no total');
      expect(lobby.hasFreeSeat(half), isTrue);

      await lobby.enter(half);
      expect(parts.adapter.calls, contains('POST /api/rooms/r1/join'));
      expect(parts.adapter.calls.where((c) => c.contains('spectate')), isEmpty);
    });

    test('a full room is watched, and the status does not enter into it', () async {
      // Both seats taken but still `Waiting` — a status-based criterion would offer a
      // seat the server must refuse.
      //
      // Positive control: dispatch on `status == playing` and this goes red.
      final parts = await build(roomJson(status: 'Waiting'));
      final lobby = await lobbyFor(parts);

      final full = Room.fromJson(jsonDecode(summaryJson(status: 'Waiting')) as Map<String, dynamic>);
      expect(full.status, RoomStatus.waiting, reason: 'precondition — it is not playing');
      expect(full.takenSeats, 2, reason: 'precondition — but it is full');
      expect(lobby.hasFreeSeat(full), isFalse);

      await lobby.enter(full);
      expect(parts.adapter.calls, contains('POST /api/rooms/r1/spectate'));
      expect(parts.adapter.calls.where((c) => c.endsWith('/join')), isEmpty);
    });

    test('with no descriptor yet, offer the seat rather than assume it is full', () async {
      // A catalogue that has not loaded is not evidence that a room is full. Offering
      // the seat gets a refusal that says something; offering "watch" silently sends a
      // player somewhere they did not ask to go.
      final parts = await build(roomJson());
      final lobby = LobbyViewModel(
        rooms: parts.rooms,
        auth: AuthRepository(dio: parts.dio, tokens: MemoryTokenStore()),
        catalog: GameCatalogRepository(parts.dio),
        gameKey: 'gomoku',
      );
      expect(lobby.hasFreeSeat(Room.fromJson(jsonDecode(summaryJson()) as Map<String, dynamic>)), isTrue);
    });
  });
}
