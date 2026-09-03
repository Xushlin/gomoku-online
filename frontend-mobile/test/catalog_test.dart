// The catalogue: what it lists, what it disables, and why one game is filtered out.
//
// Every number here was **measured against the running server**, not designed:
// `GET /api/games` returns 7 versus games, 6 of them have copy in the shared i18n
// artefact, and 1 has a board this client can draw.
import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:gewu_mobile/data/repositories/game_catalog_repository.dart';
import 'package:gewu_mobile/data/services/dio_client.dart';
import 'package:gewu_mobile/data/services/token_store.dart';
import 'package:gewu_mobile/ui/catalog/view_model/catalog_view_model.dart';
import 'package:gewu_mobile/ui/game/board_registry.dart';

/// **The real response, copied from the running backend.** A stub with three invented
/// games would prove the code handles a shape nobody serves.
const servedGames = '[\n'
    '{"gameKey":"doudizhu","isRated":false,"supportsHumanVsHuman":true,"supportsAi":false,"seatCount":3,"rows":null,"cols":null},\n'
    '{"gameKey":"gomoku","isRated":true,"supportsHumanVsHuman":true,"supportsAi":true,"seatCount":2,"rows":15,"cols":15},\n'
    '{"gameKey":"idiom-chain","isRated":true,"supportsHumanVsHuman":true,"supportsAi":false,"seatCount":2,"rows":null,"cols":null},\n'
    '{"gameKey":"tictactoe","isRated":false,"supportsHumanVsHuman":false,"supportsAi":true,"seatCount":2,"rows":3,"cols":3},\n'
    '{"gameKey":"wakeng","isRated":false,"supportsHumanVsHuman":true,"supportsAi":false,"seatCount":3,"rows":null,"cols":null},\n'
    '{"gameKey":"xiangqi","isRated":true,"supportsHumanVsHuman":true,"supportsAi":true,"seatCount":2,"rows":10,"cols":9},\n'
    '{"gameKey":"xiangqi-endgame","isRated":false,"supportsHumanVsHuman":true,"supportsAi":false,"seatCount":2,"rows":10,"cols":9}\n'
    ']';

class FixedAdapter implements HttpClientAdapter {
  FixedAdapter(this.body, [this.status = 200]);

  final String body;
  final int status;
  int calls = 0;

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    calls++;
    return ResponseBody.fromString(
      body,
      status,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

Dio dioWith(HttpClientAdapter adapter) => buildDio(
  baseUrl: 'http://example.invalid',
  tokens: MemoryTokenStore(),
  refresh: () async => false,
  adapter: adapter,
);

GameCatalogRepository repoWith(HttpClientAdapter adapter) =>
    GameCatalogRepository(dioWith(adapter));

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

List<String> servedKeys() => [
  for (final g in jsonDecode(servedGames) as List<dynamic>)
    (g as Map<String, dynamic>)['gameKey'] as String,
];

void main() {
  // The **real** bundle, not a stub: the filter this screen applies is "is there copy
  // for this game", so a stubbed bundle would be testing the stub.
  late Map<String, String> zh;

  setUpAll(() {
    zh = flatten(
      jsonDecode(File('assets/i18n/zh-CN.json').readAsStringSync())
          as Map<String, dynamic>,
    );
  });

  group('the repository', () {
    test('parses the served shape, keeping rows nullable and seatCount not', () async {
      final games = await repoWith(FixedAdapter(servedGames)).load();
      expect(games, hasLength(7));

      final chain = games.firstWhere((g) => g.gameKey == 'idiom-chain');
      expect(chain.rows, isNull, reason: '成语接龙 genuinely has no board');
      expect(chain.cols, isNull);
      expect(chain.hasBoard, isFalse);
      expect(chain.seatCount, 2, reason: 'every game with rules has a seat count');

      final xiangqi = games.firstWhere((g) => g.gameKey == 'xiangqi');
      expect(xiangqi.rows, 10);
      expect(xiangqi.cols, 9);
      expect(xiangqi.isRated, isTrue);

      // The only `supportsHumanVsHuman == false` in the registry — it is what keeps
      // any walk over this field from being one-sided.
      final ttt = games.firstWhere((g) => g.gameKey == 'tictactoe');
      expect(ttt.supportsHumanVsHuman, isFalse);
      expect(
        games.where((g) => !g.supportsHumanVsHuman),
        hasLength(1),
        reason: 'exactly one, not at least one — the day a second lands, ask why',
      );
    });

    test('fetches once and keeps it', () async {
      final adapter = FixedAdapter(servedGames);
      final repo = repoWith(adapter);
      await repo.load();
      await repo.load();
      expect(adapter.calls, 1);
    });

    test('of() answers from the catalogue and never guesses', () async {
      final repo = repoWith(FixedAdapter(servedGames));
      await repo.load();
      expect(repo.of('xiangqi')?.cols, 9);
      // A game the server did not return has no descriptor — null, not a default.
      // **A default board size is how 10×9 gets painted as 15×15.**
      expect(repo.of('klotski'), isNull);
    });

    test('a failure is a failure, not an empty catalogue', () async {
      // Both sides of `validateStatus`, because they take different paths: a 4xx comes
      // back as a *successful* response the code has to inspect, while a 5xx makes Dio
      // throw before that check runs. The first version of this test only covered the
      // 500 and caught a real leak — the repository was letting `DioException` escape.
      final serverSaidNo = repoWith(FixedAdapter('{"code":"nope"}', 403));
      await expectLater(serverSaidNo.load(), throwsA(isA<CatalogFailure>()));
      expect(serverSaidNo.of('gomoku'), isNull);

      final serverBroke = repoWith(FixedAdapter('boom', 500));
      await expectLater(serverBroke.load(), throwsA(isA<CatalogFailure>()));
      expect(serverBroke.of('gomoku'), isNull);
    });
  });

  group('the catalogue screen', () {
    Future<CatalogViewModel> loaded() async {
      final vm = CatalogViewModel(
        catalog: repoWith(FixedAdapter(servedGames)),
      );
      await vm.load(hasCopy: zh.containsKey);
      return vm;
    }

    test('the three measured numbers', () async {
      final vm = await loaded();
      // Three, not one, because each moves under a different kind of change: the
      // server registering a game, the web bundle gaining copy, and this client
      // gaining a board.
      expect(servedKeys(), hasLength(7), reason: 'served by the API');
      expect(vm.entries, hasLength(6), reason: 'have copy, so they are listed');
      // **This line has now gone red twice on purpose** — once when 象棋 landed and once
      // when 一字棋 did. The set-equality check below stayed green through both: a
      // derived invariant proves the shape, a concrete number is what makes "the number
      // moved" visible to a person. Both are needed.
      //
      // 3, not 2, and the third one added no renderer: 一字棋 shares 五子棋's.
      expect(vm.entries.where((e) => e.playable), hasLength(3), reason: 'drawable here');
    });

    test('the one filtered game is the one with no copy, and it is named', () async {
      final vm = await loaded();
      final shown = vm.entries.map((e) => e.gameKey).toSet();

      expect(servedKeys().toSet().difference(shown), {'xiangqi-endgame'});
      // And the reason, asserted rather than described.
      expect(zh.containsKey('games.xiangqi-endgame.title'), isFalse);
      expect(zh.containsKey('games.xiangqi.title'), isTrue);
    });

    test('every listed entry has copy for both of the lines it renders', () async {
      final vm = await loaded();
      final holes = [
        for (final e in vm.entries)
          if (!zh.containsKey(e.titleKey) || !zh.containsKey(e.descriptionKey)) e.gameKey,
      ];
      // The filter only checks the title. This is the other line on the card, and it
      // would render a raw key just as visibly.
      expect(holes, equals(<String>[]));
    });

    test('enabled entries are exactly the board registry', () async {
      final vm = await loaded();
      final enabled = vm.entries.where((e) => e.playable).map((e) => e.gameKey).toSet();

      // The derived invariant: **set equality**, so it stays true at one game and at
      // two. A renderer for a game the platform does not serve breaks it too, which is
      // right — that would be dead code.
      expect(enabled, boardRenderers.keys.toSet());
    });

    test('and both sides are non-empty, so neither check is vacuous', () async {
      final vm = await loaded();
      expect(vm.entries.where((e) => !e.playable), isNotEmpty);
      expect(vm.entries.where((e) => e.playable), isNotEmpty);
    });

    test('a fetch failure surfaces as a key that has copy', () async {
      final vm = CatalogViewModel(
        catalog: repoWith(FixedAdapter('{"code":"nope"}', 500)),
      );
      await vm.load(hasCopy: (_) => true);
      expect(vm.errorKey, isNotNull);
      expect(zh.containsKey(vm.errorKey), isTrue, reason: 'or the screen shows a raw key');
      expect(vm.loading, isFalse);
      expect(vm.entries, isEmpty);
    });
  });
}
