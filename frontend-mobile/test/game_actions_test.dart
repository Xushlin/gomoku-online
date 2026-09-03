// Resign and urge: when each is offered, and who decides.
//
// Both are things the server has had all along and this client had no entry for — the
// same shape as 「换不了主题」. Neither is a new capability; both were unreachable.
//
// **The urge half found a real server defect before a single button existed.**
// `test/room_social_probe_test.dart` measured that `UrgeReceived` reached nobody:
// `Clients.User(...)` needs an `IUserIdProvider` and none was registered, so every
// directed push in the platform's life went to an address with no subscriber, silently.
// See `fix-urge-user-routing`. **This file depends on that fix** — without it the
// button works and nothing ever arrives.
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

const _me = 'me-1';
const _them = 'them-1';

const _json = {
  Headers.contentTypeHeader: [Headers.jsonContentType],
};

const _games = '[{"gameKey":"gomoku","isRated":true,"supportsHumanVsHuman":true,'
    '"supportsAi":true,"seatCount":2,"rows":15,"cols":15}]';

/// A room with the seats and shape a test needs.
///
/// `seatCount` is a parameter because **the branch that depends on it cannot be
/// exercised by real data**: both games this client draws are two-seat, so
/// `canResign`'s third condition is constantly true against anything the server would
/// actually send. A criterion that is always true is an empty loop.
String roomJson({
  String status = 'Playing',
  int seatCount = 2,
  int currentSeat = 0,
  List<String?> seatedBy = const [_me, _them],
  String? result,
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
        if (seatedBy[i] != null) 'player': {'id': seatedBy[i], 'username': 'p$i'},
      },
  ],
  'host': {'id': _me, 'username': 'me'},
  'game': {
    'moves': <dynamic>[],
    'currentSeat': currentSeat,
    'result': ?result,
    'winnerUserId': ?(result == null ? null : _them),
    'endReason': ?(result == null ? null : 'Resigned'),
  },
});

/// Records every non-GET call, and can be told to refuse.
class ActionAdapter implements HttpClientAdapter {
  ActionAdapter(this.room, {this.postStatus = 200, this.postBody = '{}'});

  final String room;
  final int postStatus;
  final String postBody;
  final calls = <String>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    if (options.method != 'GET') {
      calls.add('${options.method} ${options.path}');
      return ResponseBody.fromString(postBody, postStatus, headers: _json);
    }
    if (options.path.startsWith('/api/games')) {
      return ResponseBody.fromString(_games, 200, headers: _json);
    }
    return ResponseBody.fromString(room, 200, headers: _json);
  }

  @override
  void close({bool force = false}) {}
}

/// A hub that records urges and lets a test push one back.
class UrgeHub extends MatchHub {
  UrgeHub({this.refusal}) : super(serverAddress: 'http://example.invalid', accessToken: _empty);

  static String _empty() => '';

  /// Thrown by [urge] when set — the hub reports a domain refusal as an exception whose
  /// message carries the code, with no HTTP status anywhere on that path.
  final Object? refusal;

  final urges = <String>[];
  final incoming = ValueNotifier<int>(0);
  Map<String, dynamic>? incomingPayload;
  final pushes = ValueNotifier<RoomSnapshot?>(null);
  final dissolves = ValueNotifier<int>(0);

  @override
  ValueListenable<RoomSnapshot?> get state => pushes;

  @override
  ValueListenable<int> get dissolved => dissolves;

  @override
  ValueListenable<int> get urged => incoming;

  @override
  Map<String, dynamic>? get lastUrge => incomingPayload;

  @override
  Future<void> joinRoom(String roomId) async {}

  @override
  Future<void> leaveRoom(String roomId) async {}

  @override
  Future<void> urge(String roomId) async {
    urges.add(roomId);
    if (refusal != null) throw refusal!;
  }

  /// What the server does when somebody urges this user.
  void arrive({String from = 'them'}) {
    incomingPayload = {'fromUserId': _them, 'fromUsername': from};
    incoming.value = incoming.value + 1;
  }
}

Future<({GameViewModel vm, ActionAdapter adapter, UrgeHub hub})> open(
  String room, {
  Object? urgeRefusal,
  int postStatus = 200,
  String postBody = '{}',
  String myId = _me,
}) async {
  final adapter = ActionAdapter(room, postStatus: postStatus, postBody: postBody);
  final hub = UrgeHub(refusal: urgeRefusal);
  final dio = buildDio(
    baseUrl: 'http://example.invalid',
    tokens: MemoryTokenStore(),
    refresh: () async => false,
    adapter: adapter,
  );
  final catalog = GameCatalogRepository(dio);
  await catalog.load();
  final auth = AuthRepository(dio: dio, tokens: MemoryTokenStore())
    ..currentUser = AuthUser(id: myId, username: 'me');

  final vm = GameViewModel(
    rooms: RoomRepository(dio: dio, hub: hub),
    catalog: catalog,
    auth: auth,
    roomId: 'r1',
  );
  await vm.open();
  return (vm: vm, adapter: adapter, hub: hub);
}

void main() {
  group('who may resign', () {
    test('a seated player in a playing two-seat room', () async {
      final o = await open(roomJson());
      expect(o.vm.mySeat, 0, reason: 'precondition — we are seated');
      expect(o.vm.canResign, isTrue);
    });

    test('not a spectator', () async {
      // Nobody in the seats is us.
      final o = await open(roomJson(seatedBy: const ['other-1', _them]));
      expect(o.vm.mySeat, isNull, reason: 'precondition — not seated');
      expect(o.vm.canResign, isFalse);
    });

    test('not before the game starts', () async {
      final o = await open(roomJson(status: 'Waiting', seatedBy: const [_me, null]));
      expect(o.vm.mySeat, 0, reason: 'precondition — seated, just not playing');
      expect(o.vm.canResign, isFalse);
    });

    test('not in a three-seat game, because the platform cannot name a winner', () async {
      // **The branch that real data cannot reach.** `Room.Resign` needs exactly two
      // seats; on three the API answers 409, and the web client once returned a **500**
      // on a real click for exactly this reason. Both games this client draws are
      // two-seat, so without a fabricated room this criterion is an empty loop.
      //
      // Positive control: change `== 2` to `>= 2` and only this test goes red.
      final o = await open(roomJson(seatCount: 3, seatedBy: const [_me, _them, null]));
      expect(o.vm.mySeat, 0, reason: 'precondition — seated in the three-seat room');
      expect(o.vm.room?.totalSeats, 3, reason: 'precondition — the room says three');
      expect(o.vm.canResign, isFalse);
    });

    test('the seat count comes from the room, not from the catalogue', () async {
      // The catalogue says gomoku has 2 seats; the room says 3. The room wins, because
      // it is this room being resigned.
      final o = await open(roomJson(seatCount: 3, seatedBy: const [_me, _them, null]));
      expect(o.vm.descriptor?.seatCount, 2, reason: 'precondition — they disagree');
      expect(o.vm.canResign, isFalse);
    });
  });

  group('resigning', () {
    test('calls the route the controller actually has', () async {
      final o = await open(roomJson());
      await o.vm.resign();
      expect(o.adapter.calls, contains('POST /api/rooms/r1/resign'));
    });

    test('and writes down nothing about the outcome', () async {
      // **The whole point.** A client that recorded "I lost" here would be a second
      // path announcing a result, and the way two such paths fail is one of them
      // naming the wrong winner. The result must still come only from the snapshot.
      //
      // Positive control: set an outcome inside `resign()` and this goes red.
      final o = await open(roomJson());
      expect(o.vm.outcome, isNull, reason: 'precondition — the game is still on');
      await o.vm.resign();
      expect(o.vm.outcome, isNull, reason: 'the server has not said anything yet');

      // …and when the server does say so, it appears.
      o.hub.pushes.value = RoomSnapshot(
        jsonDecode(roomJson(status: 'Finished', result: 'Decided')) as Map<String, dynamic>,
      );
      expect(o.vm.outcome?.titleKey, 'game.ended.title-lose');
      expect(o.vm.outcome?.reasonKey, 'game.ended.reason-resigned');
    });

    test('a refusal surfaces as an error rather than silence', () async {
      final o = await open(roomJson(), postStatus: 409, postBody: '{"code":"nope"}');
      await o.vm.resign();
      expect(o.vm.errorKey, isNotNull);
    });
  });

  group('when urging is offered and when it can be pressed', () {
    test('offered to a seated player in a playing room', () async {
      final o = await open(roomJson(currentSeat: 1));
      expect(o.vm.canUrge, isTrue);
      expect(o.vm.urgeDisabledReasonKey, isNull, reason: 'it is their turn, so press away');
    });

    test('not offered to a spectator', () async {
      final o = await open(roomJson(seatedBy: const ['other-1', _them]));
      expect(o.vm.canUrge, isFalse);
    });

    test('offered but not pressable on your own turn, with the reason said out loud',
        () async {
      final o = await open(roomJson(currentSeat: 0));
      expect(o.vm.canUrge, isTrue, reason: 'the button is there…');
      expect(o.vm.urgeDisabledReasonKey, 'game.urge.button-disabled-own-turn');
    });

    test('a 429 becomes the cooldown copy, not the generic error', () async {
      final o = await open(
        roomJson(currentSeat: 1),
        urgeRefusal: const RoomFailure('UrgeTooFrequent', 429),
      );
      expect(o.vm.urgeDisabledReasonKey, isNull, reason: 'precondition — pressable');

      await o.vm.urge();
      expect(o.hub.urges, ['r1'], reason: 'it was actually sent');
      expect(o.vm.errorKey, 'game.errors.urge-cooldown');
      expect(o.vm.urgeDisabledReasonKey, 'game.urge.button-disabled-cooldown');
    });

    test('a refusal that is not a cooldown does not claim to be one', () async {
      // Without this, mapping *every* failure to the cooldown copy would pass the test
      // above — and telling somebody to wait 30 seconds for a connection error is a
      // wrong answer that looks like a right one.
      final o = await open(
        roomJson(currentSeat: 1),
        urgeRefusal: const RoomFailure('generic', 500),
      );
      await o.vm.urge();
      expect(o.vm.errorKey, 'game.errors.generic');
      expect(o.vm.urgeDisabledReasonKey, isNull);
    });

    test('a successful urge is sent and leaves the button usable', () async {
      final o = await open(roomJson(currentSeat: 1));
      await o.vm.urge();
      expect(o.hub.urges, ['r1']);
      expect(o.vm.errorKey, isNull);
    });
  });

  group('being urged', () {
    test('the count follows the push, and twice is twice', () async {
      // **A counter, not a flag.** Being urged again has to be visible again; a bool
      // that is already true reports nothing the second time.
      final o = await open(roomJson(currentSeat: 0));
      expect(o.vm.urgeCount, 0, reason: 'precondition');

      o.hub.arrive();
      expect(o.vm.urgeCount, 1);
      expect(o.vm.urgedBy, 'them');

      o.hub.arrive();
      expect(o.vm.urgeCount, 2);
    });

    test('nothing about it is in the snapshot', () async {
      // The server never writes "you have been urged" into `RoomStateDto`, so an
      // implementation that re-read the room to find out would find nothing forever.
      // This asserts the shape of the data, which is why the push is the only route.
      final snapshot = jsonDecode(roomJson()) as Map<String, dynamic>;
      expect(snapshot.keys.where((k) => k.toLowerCase().contains('urge')), isEmpty);
      expect(
        (snapshot['game'] as Map<String, dynamic>).keys
            .where((k) => k.toLowerCase().contains('urge')),
        isEmpty,
      );
    });

    test('the listener is removed on dispose', () async {
      final o = await open(roomJson(currentSeat: 0));
      o.hub.arrive();
      expect(o.vm.urgeCount, 1, reason: 'precondition — it was live');

      o.vm.dispose();
      o.hub.arrive();
      expect(o.vm.urgeCount, 1, reason: 'and stops once the screen is gone');
    });
  });
}
