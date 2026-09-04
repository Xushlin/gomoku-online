// The two lobby entries, and the AI room they lead to.
//
// **Both entries are derived from the descriptor, and this lobby already had the bug
// that rule exists to prevent:** the create-room button was unconditional, while
// `POST /api/rooms {"gameKey":"tictactoe"}` answers
// *400 'tictactoe' has no human-vs-human mode on this platform.* It was out of reach
// only because 一字棋 had no board yet — giving it one is what makes it reachable, so
// the two land together.
import 'dart:convert';
import 'dart:io';
import 'dart:ui';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:gewu_mobile/data/repositories/auth_repository.dart';
import 'package:gewu_mobile/data/repositories/game_catalog_repository.dart';
import 'package:gewu_mobile/data/repositories/room_repository.dart';
import 'package:gewu_mobile/data/services/dio_client.dart';
import 'package:gewu_mobile/data/services/match_hub_service.dart';
import 'package:gewu_mobile/data/services/token_store.dart';
import 'package:gewu_mobile/theme/app_theme.dart';
import 'package:gewu_mobile/theme/board_skin.dart';
import 'package:gewu_mobile/ui/game/board_registry.dart';
import 'package:gewu_mobile/ui/game/view/board_geometry.dart';
import 'package:gewu_mobile/ui/game/view/board_renderer.dart';
import 'package:gewu_mobile/ui/lobby/view_model/lobby_view_model.dart';

/// **The real `GET /api/games` response**, copied from the running backend.
const servedGames = '[\n'
    '{"gameKey":"gomoku","isRated":true,"supportsHumanVsHuman":true,"supportsAi":true,"seatCount":2,"rows":15,"cols":15},\n'
    '{"gameKey":"idiom-chain","isRated":true,"supportsHumanVsHuman":true,"supportsAi":false,"seatCount":2,"rows":null,"cols":null},\n'
    '{"gameKey":"tictactoe","isRated":false,"supportsHumanVsHuman":false,"supportsAi":true,"seatCount":2,"rows":3,"cols":3},\n'
    '{"gameKey":"xiangqi","isRated":true,"supportsHumanVsHuman":true,"supportsAi":true,"seatCount":2,"rows":10,"cols":9}\n'
    ']';

class RecordingAdapter implements HttpClientAdapter {
  RecordingAdapter({this.postStatus = 201});

  final int postStatus;
  final calls = <String>[];
  final bodies = <Object?>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    if (options.path.startsWith('/api/games')) {
      return ResponseBody.fromString(servedGames, 200, headers: _json);
    }
    if (options.method != 'GET') {
      calls.add('${options.method} ${options.path}');
      bodies.add(options.data);
      return ResponseBody.fromString(
        '{"id":"ai-1","name":"n","gameKey":"tictactoe","status":"Playing","seats":[],'
        '"seatCount":2,"game":{"moves":[],"currentSeat":0}}',
        postStatus,
        headers: _json,
      );
    }
    return ResponseBody.fromString('{}', 200, headers: _json);
  }

  @override
  void close({bool force = false}) {}
}

const _json = {
  Headers.contentTypeHeader: [Headers.jsonContentType],
};

Future<({LobbyViewModel vm, RecordingAdapter adapter})> lobbyFor(
  String gameKey, {
  int postStatus = 201,
}) async {
  final adapter = RecordingAdapter(postStatus: postStatus);
  final dio = buildDio(
    baseUrl: 'http://example.invalid',
    tokens: MemoryTokenStore(),
    refresh: () async => false,
    adapter: adapter,
  );
  final catalog = GameCatalogRepository(dio);
  final vm = LobbyViewModel(
    rooms: RoomRepository(
      dio: dio,
      hub: MatchHub(serverAddress: 'http://example.invalid', accessToken: () => ''),
    ),
    auth: AuthRepository(dio: dio, tokens: MemoryTokenStore()),
    catalog: catalog,
    gameKey: gameKey,
  );
  await vm.load();
  return (vm: vm, adapter: adapter);
}

/// The default skin, resolved once. These tests are about geometry, not colour — but
/// the painter needs a skin, and a fake one would be a second source of truth.
BoardSkin testSkin() => BoardSkin.resolve(
  skinName: BoardSkin.defaultSkinName,
  themeName: defaultThemeName,
  brightness: Brightness.dark,
);

void main() {
  group('one renderer, two game keys', () {
    test('一字棋 and 五子棋 resolve to one renderer', () {
      expect(
        identical(boardRenderers[tictactoeGameKey], boardRenderers[gomokuGameKey]),
        isTrue,
        reason: '一字棋 is 五子棋 with three roads, per the server and the platform',
      );
      // And 象棋 does not share it, or "they are all the same" would be trivially true.
      expect(
        identical(boardRenderers[xiangqiGameKey], boardRenderers[gomokuGameKey]),
        isFalse,
      );
    });

    test('and what that assertion can and cannot see', () {
      // **Measured, and it changed what the test above claims.** Writing
      // `GomokuRenderer()` inline instead of the shared `_nInARow` constant leaves
      // `identical` still true — Dart canonicalises equal `const` expressions. So the
      // assertion above pins the *fact callers rely on* ("these two keys give you one
      // renderer") and says nothing about how the map is written. The control that does
      // fail is pointing 一字棋 at a different renderer, which is the mistake worth
      // catching anyway.
      expect(
        identical(const GomokuRenderer(), const GomokuRenderer()),
        isTrue,
        reason: 'const canonicalisation — this is why the naming is a readability '
            'choice, not something a test can enforce',
      );
    });

    test('so the registry has three keys and two distinct renderers', () {
      expect(boardRenderers, hasLength(3));
      expect(boardRenderers.values.toSet(), hasLength(2));
    });

    test('3x3 derives no star points and stays inside the board', () async {
      final renderer = boardRenderers[tictactoeGameKey]!;
      final g = BoardGeometry.fit(rows: 3, cols: 3, canvas: const Size(300, 300));
      expect(GomokuRenderer.starLines(3, 3), isEmpty);

      // Sampled as pixels, the same way the 象棋 board is checked — re-deriving the
      // coordinates beside the code that derives them checks nothing.
      final recorder = PictureRecorder();
      final canvas = Canvas(recorder);
      renderer.paintDecoration(canvas, g, testSkin());
      final image = await recorder.endRecording().toImage(300, 300);
      final bytes = (await image.toByteData())!;

      var inked = 0;
      var strays = 0;
      for (var y = 0; y < 300; y++) {
        for (var x = 0; x < 300; x++) {
          if (bytes.getUint8((y * 300 + x) * 4 + 3) == 0) continue;
          inked++;
          if (x < g.originDx - 2 ||
              x > g.originDx + g.width + 2 ||
              y < g.originDy - 2 ||
              y > g.originDy + g.height + 2) {
            strays++;
          }
        }
      }
      expect(inked, greaterThan(100), reason: 'the grid must actually be drawn');
      expect(strays, 0, reason: '$strays inked pixels outside a 3x3 board');
    });
  });

  group('the lobby entries are derived, not listed', () {
    test('一字棋: no create-room, yes AI', () async {
      final lobby = await lobbyFor(tictactoeGameKey);
      expect(lobby.vm.canCreateRoom, isFalse, reason: 'the server answers 400 for it');
      expect(lobby.vm.canPlayAi, isTrue);
    });

    test('五子棋: both', () async {
      // The other direction. Without it, an implementation that always hides
      // create-room passes the test above — and it would break the only game that ships.
      final lobby = await lobbyFor(gomokuGameKey);
      expect(lobby.vm.canCreateRoom, isTrue);
      expect(lobby.vm.canPlayAi, isTrue);
    });

    test('成语接龙: create-room but no AI', () async {
      // The third combination, and the one that keeps "canPlayAi" from being a
      // restatement of "canCreateRoom".
      final lobby = await lobbyFor('idiom-chain');
      expect(lobby.vm.canCreateRoom, isTrue);
      expect(lobby.vm.canPlayAi, isFalse);
    });

    test('an unknown game offers neither', () async {
      final lobby = await lobbyFor('klotski');
      expect(lobby.vm.descriptor, isNull);
      expect(lobby.vm.canCreateRoom, isFalse);
      expect(lobby.vm.canPlayAi, isFalse);
    });
  });

  group('creating the AI room', () {
    test('goes to POST /api/rooms/ai with the server spellings', () async {
      final lobby = await lobbyFor(tictactoeGameKey);
      final id = await lobby.vm.createAiRoom(difficulty: 'Hard', humanSide: 'White');

      expect(id, 'ai-1');
      expect(lobby.adapter.calls, ['POST /api/rooms/ai']);
      final body = jsonDecode(jsonEncode(lobby.adapter.bodies.single)) as Map<String, dynamic>;
      expect(body['gameKey'], tictactoeGameKey);
      expect(body['difficulty'], 'Hard');
      expect(body['humanSide'], 'White');
      expect(body['name'], isNotEmpty);
    });

    test('a refusal surfaces one generic key, because the 400 is a binding error', () async {
      // A wrong difficulty comes back as `"The body field is required"` plus a JSON
      // conversion failure on `$.difficulty` — there is no field-level message in it.
      final lobby = await lobbyFor(tictactoeGameKey, postStatus: 400);
      final id = await lobby.vm.createAiRoom(difficulty: 'Impossible', humanSide: 'Black');

      expect(id, isNull);
      expect(lobby.vm.errorKey, 'lobby.ai-game.errors.generic');
      expect(lobby.adapter.calls, hasLength(1), reason: 'precondition — it was sent');
    });
  });

  group('every key these screens name has copy', () {
    test('in both locales', () {
      const keys = [
        'lobby.ai-game.button',
        'lobby.ai-game.dialog-title',
        'lobby.ai-game.difficulty-label',
        'lobby.ai-game.difficulty-easy',
        'lobby.ai-game.difficulty-medium',
        'lobby.ai-game.difficulty-hard',
        'lobby.ai-game.side-label',
        'lobby.ai-game.side-black',
        'lobby.ai-game.side-white',
        'lobby.ai-game.submit',
        'lobby.ai-game.cancel',
        'lobby.ai-game.errors.generic',
        'lobby.game-lobby.unavailable.ai-only-title',
        'lobby.game-lobby.unavailable.ai-only-body',
        'games.$tictactoeGameKey.title',
      ];
      for (final locale in const ['zh-CN', 'en']) {
        final bundle = _flatten(
          jsonDecode(File('assets/i18n/$locale.json').readAsStringSync())
              as Map<String, dynamic>,
        );
        final missing = [for (final k in keys) if (!bundle.containsKey(k)) k];
        expect(missing, equals(<String>[]), reason: locale);
      }
    });
  });
}

Map<String, String> _flatten(Map<String, dynamic> json, [String prefix = '']) {
  final out = <String, String>{};
  json.forEach((key, value) {
    final path = prefix.isEmpty ? key : '$prefix.$key';
    if (value is Map<String, dynamic>) {
      out.addAll(_flatten(value, path));
    } else {
      out[path] = '$value';
    }
  });
  return out;
}
