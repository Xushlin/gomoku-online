// Leaving a room: which route, whether to ask, and what stops listening.
//
// **The client never left a room at all before this.** Searching `lib/` for
// `leave`/`dissolve` found seven hits, all of them prose in comments; the hub's
// `LeaveRoom` was called zero times. Backing out popped a route and nothing else, so
// server-side you stayed in the seat forever.
//
// The headline test here is `a push for a room we left must not repaint the one we are
// in`. **It could not have failed before `fix-mobile-hub-inbound`** — no push ever
// arrived — which is why it is written now and not then: *a conclusion can stay true
// after the premise that held it up became false.*
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
import 'package:gewu_mobile/ui/game/board_registry.dart';
import 'package:gewu_mobile/data/repositories/settings_repository.dart';
import 'package:gewu_mobile/data/repositories/sound_repository.dart';
import 'package:gewu_mobile/data/services/preferences_store.dart';
import 'package:gewu_mobile/data/services/sound_player.dart';
import 'package:gewu_mobile/ui/game/view_model/game_view_model.dart';

/// A hub that records what it was asked to do and lets a test push state in.
class PushableHub extends MatchHub {
  PushableHub() : super(serverAddress: 'http://example.invalid', accessToken: _empty);

  static String _empty() => '';

  final pushes = ValueNotifier<RoomSnapshot?>(null);
  final dissolves = ValueNotifier<int>(0);
  final joined = <String>[];
  final left = <String>[];

  @override
  ValueListenable<RoomSnapshot?> get state => pushes;

  @override
  ValueListenable<int> get dissolved => dissolves;

  @override
  Future<void> joinRoom(String roomId) async => joined.add(roomId);

  @override
  Future<void> leaveRoom(String roomId) async => left.add(roomId);
}

/// Answers `GET /api/rooms/{id}` with a room of that id, and records every POST path.
class RoomAdapter implements HttpClientAdapter {
  RoomAdapter({this.status = 'Waiting', this.hostId = 'host-1', this.postStatus = 200});

  final String status;
  final String hostId;
  final int postStatus;

  /// `VERB path`, so a test can tell `POST /leave` from `DELETE /rooms/{id}` — the
  /// distinction the first version of this fake could not make, which is how it happily
  /// confirmed a route the server does not have.
  final calls = <String>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    if (options.method != 'GET') {
      calls.add('${options.method} ${options.path}');
      return ResponseBody.fromString('{}', postStatus, headers: _json);
    }
    if (options.path.startsWith('/api/games')) {
      return ResponseBody.fromString(_games, 200, headers: _json);
    }
    final id = options.path.split('/').last;
    return ResponseBody.fromString(roomJson(id, status: status, hostId: hostId), 200,
        headers: _json);
  }

  @override
  void close({bool force = false}) {}
}

const _json = {
  Headers.contentTypeHeader: [Headers.jsonContentType],
};

const _games = '[{"gameKey":"gomoku","isRated":true,"supportsHumanVsHuman":true,'
    '"supportsAi":true,"seatCount":2,"rows":15,"cols":15}]';

String roomJson(String id, {String status = 'Waiting', String hostId = 'host-1'}) =>
    '{"id":"$id","name":"room-$id","gameKey":"gomoku","status":"$status",'
    '"seats":[],"seatCount":2,"host":{"id":"$hostId","username":"h"},'
    '"game":{"moves":[],"currentSeat":0}}';

({RoomRepository rooms, PushableHub hub, RoomAdapter adapter}) build({
  String status = 'Waiting',
  String hostId = 'host-1',
  int postStatus = 200,
}) {
  final adapter = RoomAdapter(status: status, hostId: hostId, postStatus: postStatus);
  final hub = PushableHub();
  final dio = buildDio(
    baseUrl: 'http://example.invalid',
    tokens: MemoryTokenStore(),
    refresh: () async => false,
    adapter: adapter,
  );
  return (rooms: RoomRepository(dio: dio, hub: hub), hub: hub, adapter: adapter);
}

Future<GameViewModel> viewModelFor(
  ({RoomRepository rooms, PushableHub hub, RoomAdapter adapter}) parts,
  String roomId, {
  String? myId,
}) async {
  final dio = buildDio(
    baseUrl: 'http://example.invalid',
    tokens: MemoryTokenStore(),
    refresh: () async => false,
    adapter: parts.adapter,
  );
  final catalog = GameCatalogRepository(dio);
  await catalog.load();
  final auth = AuthRepository(dio: dio, tokens: MemoryTokenStore());
  if (myId != null) auth.currentUser = AuthUser(id: myId, username: 'me');

  final vm = GameViewModel(
    rooms: parts.rooms,
    catalog: catalog,
    auth: auth,
    sound: recordingSound(),
    roomId: roomId,
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
  group('the listener is registered once, not once per room', () {
    test('two opens produce one republish per push', () async {
      final parts = build();
      await parts.rooms.open('A');
      await parts.rooms.open('A');

      var notifications = 0;
      parts.rooms.live.addListener(() => notifications++);
      parts.hub.pushes.value = RoomSnapshot(
        Map<String, dynamic>.from(_decode(roomJson('A'))),
      );

      // Before this change `open` called `addListener` every time and nothing ever
      // removed it: two opens meant two registrations and two parses per push. Harmless,
      // because republishing is idempotent — **and harmless is how a thing like this
      // survives.**
      expect(notifications, 1);
    });
  });

  group('a push for a room we left must not repaint the one we are in', () {
    test('enter A, leave, enter B, a move lands in A', () async {
      final parts = build();
      await parts.rooms.open('A');
      await parts.rooms.leave('A', asHostOfWaitingRoom: false);
      await parts.rooms.open('B');

      // Someone moves in A. We are in B.
      parts.hub.pushes.value = RoomSnapshot(
        Map<String, dynamic>.from(_decode(roomJson('A'))),
      );

      expect(parts.rooms.live.value?.id, 'B', reason: 'B must not show A');
      expect(parts.hub.left, ['A'], reason: 'and we must have left A on the hub');
    });

    test('a push for the room we ARE in still lands', () async {
      // The other direction. Without it, an implementation that ignores every push
      // passes the test above.
      final parts = build();
      await parts.rooms.open('B');
      parts.hub.pushes.value = RoomSnapshot(
        Map<String, dynamic>.from(_decode(roomJson('B', status: 'Playing'))),
      );
      expect(parts.rooms.live.value?.status, RoomStatus.playing);
    });
  });

  group('which route leaving takes is the server rule, not a preference', () {
    test('the host of a waiting room dissolves', () async {
      final parts = build(status: 'Waiting', hostId: 'me');
      final vm = await viewModelFor(parts, 'A', myId: 'me');

      expect(vm.leavingDissolves, isTrue);
      expect(await vm.leave(), isTrue);
      // **The measured route.** `DELETE /api/rooms/{id}` — there is no
      // `/dissolve` path; posting to one returns 404 from the real server.
      expect(parts.adapter.calls, ['DELETE /api/rooms/A']);
    });

    test('anybody else leaves', () async {
      // The other direction, and it matters: `/dissolve` in somebody else's room is
      // refused by the server, so an implementation that always dissolves would pass
      // the test above and break in the field.
      final parts = build(status: 'Waiting', hostId: 'someone-else');
      final vm = await viewModelFor(parts, 'A', myId: 'me');

      expect(vm.leavingDissolves, isFalse);
      expect(await vm.leave(), isTrue);
      expect(parts.adapter.calls, ['POST /api/rooms/A/leave']);
    });

    test('the host of a PLAYING room leaves, it does not dissolve', () async {
      // `/dissolve` exists only for waiting rooms. Being the host is not enough.
      final parts = build(status: 'Playing', hostId: 'me');
      final vm = await viewModelFor(parts, 'A', myId: 'me');

      expect(vm.leavingDissolves, isFalse);
      expect(await vm.leave(), isTrue);
      expect(parts.adapter.calls, ['POST /api/rooms/A/leave']);
    });

    test('identity is the id, not the username', () async {
      // A username is a display name. Two of this platform's bugs came from treating
      // one as an identity, so this pins that a matching *name* is not enough.
      final parts = build(status: 'Waiting', hostId: 'host-1');
      final vm = await viewModelFor(parts, 'A', myId: 'not-host-1');
      expect(vm.leavingDissolves, isFalse);
    });
  });

  group('asking before leaving', () {
    test('a game in play warns', () async {
      final parts = build(status: 'Playing');
      final vm = await viewModelFor(parts, 'A');
      expect(vm.leavingNeedsConfirmation, isTrue);
      expect(vm.leaveWarningKey, 'game.leave-confirm.match');
    });

    test('a waiting room does not', () async {
      // Both directions. Without this half, an implementation that always asks passes
      // the test above — and asking to leave an empty room you just made is noise.
      final parts = build(status: 'Waiting');
      final vm = await viewModelFor(parts, 'A');
      expect(vm.leavingNeedsConfirmation, isFalse);
      expect(vm.leaveWarningKey, isNull);
    });
  });

  group('a refusal must not look like a successful exit', () {
    test('leave() returns false and sets an error when the server says no', () async {
      final parts = build(postStatus: 403);
      final vm = await viewModelFor(parts, 'A');

      expect(await vm.leave(), isFalse);
      expect(vm.errorKey, isNotNull);
      // The precondition, so this is not green because the call never happened.
      expect(parts.adapter.calls, hasLength(1));
    });

    test('a 404 counts as gone, because that is the outcome asked for', () async {
      final parts = build(postStatus: 404);
      final vm = await viewModelFor(parts, 'A');
      expect(await vm.leave(), isTrue);
    });
  });

  group('a dissolved room is an exit, because no further state will arrive', () {
    test('the view model learns about it', () async {
      final parts = build();
      final vm = await viewModelFor(parts, 'A');
      expect(vm.wasDissolved, isFalse, reason: 'precondition');

      parts.hub.dissolves.value = 1;
      expect(vm.wasDissolved, isTrue);
    });

    test('and 五子棋 is still the game the exit routes back to', () async {
      final parts = build();
      final vm = await viewModelFor(parts, 'A');
      expect(vm.room?.gameKey, gomokuGameKey);
    });
  });
}

Map<String, dynamic> _decode(String json) =>
    Map<String, dynamic>.from(jsonDecode(json) as Map);
