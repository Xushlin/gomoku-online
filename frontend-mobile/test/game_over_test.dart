// Saying who won.
//
// **Found on a real phone: a finished game just stopped.** The board stayed, every tap
// was refused by the server, and nothing said anything. The data had arrived **twice**
// and been dropped twice — the server puts `result` / `winnerUserId` / `endReason` in
// every snapshot, and the client parsed neither those nor the `GameEnded` push it had
// subscribed to and piped into a stream with no consumer.
import 'dart:convert';
import 'dart:io';

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
import 'package:gewu_mobile/ui/game/view_model/game_view_model.dart';

const _me = 'me-1';
const _them = 'them-1';

const _games = '[{"gameKey":"gomoku","isRated":true,"supportsHumanVsHuman":true,'
    '"supportsAi":true,"seatCount":2,"rows":15,"cols":15}]';

/// A room snapshot in whatever end state the test needs.
String roomJson({String? result, String? winner, String? reason}) => jsonEncode({
  'id': 'r1',
  'name': 'room',
  'gameKey': gomokuGameKey,
  'status': result == null ? 'Playing' : 'Finished',
  'seats': <dynamic>[],
  'seatCount': 2,
  'game': {
    'moves': <dynamic>[],
    'currentSeat': 0,
    'result': ?result,
    'winnerUserId': ?winner,
    'endReason': ?reason,
  },
});

class FixedAdapter implements HttpClientAdapter {
  FixedAdapter(this.room);

  final String room;

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    if (options.path.startsWith('/api/games')) {
      return ResponseBody.fromString(_games, 200, headers: _json);
    }
    return ResponseBody.fromString(room, 200, headers: _json);
  }

  @override
  void close({bool force = false}) {}
}

const _json = {
  Headers.contentTypeHeader: [Headers.jsonContentType],
};

Future<GameViewModel> viewModelFor(String room) async {
  final dio = buildDio(
    baseUrl: 'http://example.invalid',
    tokens: MemoryTokenStore(),
    refresh: () async => false,
    adapter: FixedAdapter(room),
  );
  final catalog = GameCatalogRepository(dio);
  await catalog.load();
  final auth = AuthRepository(dio: dio, tokens: MemoryTokenStore())
    ..currentUser = const AuthUser(id: _me, username: 'me');

  final vm = GameViewModel(
    rooms: RoomRepository(
      dio: dio,
      hub: MatchHub(serverAddress: 'http://example.invalid', accessToken: () => ''),
    ),
    catalog: catalog,
    auth: auth,
    roomId: 'r1',
  );
  vm.room = Room.fromJson(jsonDecode(room) as Map<String, dynamic>);
  return vm;
}

Map<String, String> flatten(Map<String, dynamic> json, [String prefix = '']) {
  final out = <String, String>{};
  json.forEach((key, value) {
    final path = prefix.isEmpty ? key : '$prefix.$key';
    if (value is Map<String, dynamic>) {
      out.addAll(flatten(value, path));
    } else {
      out[path] = '$value';
    }
  });
  return out;
}

void main() {
  group('the snapshot carries what the server always sent', () {
    test('result, winner and reason are parsed', () {
      final room = Room.fromJson(
        jsonDecode(roomJson(result: 'Decided', winner: _me, reason: 'Resigned'))
            as Map<String, dynamic>,
      );
      expect(room.game.result, GameResult.decided);
      expect(room.game.winnerUserId, _me);
      expect(room.game.endReason, GameEndReason.resigned);
      expect(room.game.isOver, isTrue);
    });

    test('an ongoing game has none of them', () {
      final room = Room.fromJson(jsonDecode(roomJson()) as Map<String, dynamic>);
      expect(room.game.result, GameResult.ongoing);
      expect(room.game.winnerUserId, isNull);
      expect(room.game.endReason, isNull);
      expect(room.game.isOver, isFalse);
    });

    test('parsed by name, because the server enum skips 2', () {
      // `Ongoing = 0`, `Decided = 1`, `Draw = 3`. Reading ordinals here would copy a
      // gap; reading names cannot.
      expect(GameResult.parse('Draw'), GameResult.draw);
      expect(GameResult.parse('Decided'), GameResult.decided);
      expect(GameResult.parse('Ongoing'), GameResult.ongoing);
      expect(GameResult.parse('Something'), GameResult.unknown);
      // And the two nulls are different things: a snapshot with no `result` is an
      // ongoing game (handled in `fromJson`), while an unrecognised string is
      // `unknown` — the case where this client must not guess.
      expect(GameResult.parse(null), GameResult.unknown);
    });
  });

  group('who won', () {
    test('I won', () async {
      final vm = await viewModelFor(
        roomJson(result: 'Decided', winner: _me, reason: 'Decided'),
      );
      expect(vm.outcome?.titleKey, 'game.ended.title-win');
      expect(vm.outcome?.reasonKey, 'game.ended.reason-decided');
    });

    test('I lost', () async {
      // The other direction. Without it, an implementation that always says "you won"
      // passes the test above — and that is the worst possible version of this feature.
      final vm = await viewModelFor(
        roomJson(result: 'Decided', winner: _them, reason: 'Decided'),
      );
      expect(vm.outcome?.titleKey, 'game.ended.title-lose');
    });

    test('a draw is neither', () async {
      final vm = await viewModelFor(roomJson(result: 'Draw', reason: 'Decided'));
      expect(vm.outcome?.titleKey, 'game.ended.title-draw');
    });

    test('a game still on says nothing', () async {
      // And this is the half that keeps the three above from being an "always announce"
      // implementation.
      final vm = await viewModelFor(roomJson());
      expect(vm.outcome, isNull);
    });

    test('decided with no winner recorded is not a win', () async {
      // A `Decided` with a null winner should not read as "I won" just because the
      // comparison happens to be null == null.
      final vm = await viewModelFor(roomJson(result: 'Decided', reason: 'Decided'));
      expect(vm.outcome?.titleKey, 'game.ended.title-lose');
    });

    test('identity is the id, not the username', () async {
      // The username in the fixture is 'me' for both; only the id differs.
      final vm = await viewModelFor(
        roomJson(result: 'Decided', winner: 'someone-else', reason: 'Decided'),
      );
      expect(vm.outcome?.titleKey, 'game.ended.title-lose');
    });
  });

  group('why it ended', () {
    for (final (wire, key) in const [
      ('Decided', 'game.ended.reason-decided'),
      ('Resigned', 'game.ended.reason-resigned'),
      ('TurnTimeout', 'game.ended.reason-timeout'),
    ]) {
      test('$wire -> $key', () async {
        final vm = await viewModelFor(
          roomJson(result: 'Decided', winner: _me, reason: wire),
        );
        expect(vm.outcome?.reasonKey, key);
      });
    }

    test('an unknown reason says nothing rather than guessing', () async {
      final vm = await viewModelFor(
        roomJson(result: 'Decided', winner: _me, reason: 'Something'),
      );
      expect(vm.outcome?.titleKey, 'game.ended.title-win');
      expect(vm.outcome?.reasonKey, isNull);
    });
  });

  group('announced once', () {
    test('dismissing sticks', () async {
      final vm = await viewModelFor(
        roomJson(result: 'Decided', winner: _me, reason: 'Decided'),
      );
      expect(vm.outcomeDismissed, isFalse, reason: 'precondition');
      vm.dismissOutcome();
      expect(vm.outcomeDismissed, isTrue);
      // The outcome itself is still computable — the View decides whether to show it.
      expect(vm.outcome, isNotNull);
    });
  });

  group('every key this can produce has copy', () {
    test('in both locales', () {
      const keys = [
        'game.ended.title-win',
        'game.ended.title-lose',
        'game.ended.title-draw',
        'game.ended.reason-decided',
        'game.ended.reason-resigned',
        'game.ended.reason-timeout',
        'game.ended.back-to-lobby',
        'game.ended.dismiss',
      ];
      for (final locale in const ['zh-CN', 'en']) {
        final bundle = flatten(
          jsonDecode(File('assets/i18n/$locale.json').readAsStringSync())
              as Map<String, dynamic>,
        );
        expect(
          [for (final k in keys) if (!bundle.containsKey(k)) k],
          equals(<String>[]),
          reason: '$locale — a result box rendering a raw key is worse than none',
        );
      }
    });
  });

  group('the stream nobody consumed is gone', () {
    test('the hub service has no GameEnded subscription and no error stream', () {
      final source = File('lib/data/services/match_hub_service.dart').readAsStringSync();
      expect(source.contains("connection.on('GameEnded'"), isFalse);
      expect(source.contains('StreamController'), isFalse);
      // Non-vacuity: the file is the one we think it is and still subscribes to the
      // thing that carries the result.
      expect(source.contains("connection.on('RoomState'"), isTrue);
    });
  });
}
