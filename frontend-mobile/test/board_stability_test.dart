// The board must not move when the game does.
//
// **Reported from a real phone: the board flickers every time a stone is placed** — in
// both 五子棋 and 象棋, which is the first clue that it has nothing to do with either
// game's renderer.
//
// It does not: the action bar under the board carries a line saying *why* the urge
// button cannot be pressed, and the reason it carries — 「现在是你的回合」 — is true on
// your turn and false on your opponent's. So the line appears and disappears on **every
// single ply**, the column re-lays out, and the board (inside an `Expanded`, centred)
// shifts by the height of one line of text.
//
// This file mounts `GameView` on its own, which nothing did before: the screen's layout
// had only ever been exercised through integration tests that drive the whole app and
// never compare a rect across a turn change.
import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart' show rootBundle;
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'package:gewu_mobile/data/models/models.dart';
import 'package:gewu_mobile/data/repositories/auth_repository.dart';
import 'package:gewu_mobile/data/repositories/game_catalog_repository.dart';
import 'package:gewu_mobile/data/repositories/room_repository.dart';
import 'package:gewu_mobile/data/repositories/settings_repository.dart';
import 'package:gewu_mobile/data/repositories/sound_repository.dart';
import 'package:gewu_mobile/data/services/dio_client.dart';
import 'package:gewu_mobile/data/services/match_hub_service.dart';
import 'package:gewu_mobile/data/services/preferences_store.dart';
import 'package:gewu_mobile/data/services/sound_player.dart';
import 'package:gewu_mobile/data/services/token_store.dart';
import 'package:gewu_mobile/i18n/translations.dart';
import 'package:gewu_mobile/theme/app_theme.dart';
import 'package:gewu_mobile/ui/game/view/game_board.dart';
import 'package:gewu_mobile/ui/game/view/game_view.dart';
import 'package:gewu_mobile/ui/game/view_model/game_view_model.dart';

const _me = 'me-1';
const _them = 'them-1';

const _json = {
  Headers.contentTypeHeader: [Headers.jsonContentType],
};

const _games = '[{"gameKey":"gomoku","isRated":true,"supportsHumanVsHuman":true,'
    '"supportsAi":true,"seatCount":2,"rows":15,"cols":15},'
    '{"gameKey":"xiangqi","isRated":true,"supportsHumanVsHuman":true,'
    '"supportsAi":true,"seatCount":2,"rows":10,"cols":9}]';

/// A room in play. [currentSeat] is what flips on every ply.
String roomJson({
  String gameKey = 'gomoku',
  int currentSeat = 0,
  List<Map<String, int>> moves = const [],
}) => jsonEncode({
  'id': 'r1',
  'name': 'room',
  'gameKey': gameKey,
  'status': 'Playing',
  'seatCount': 2,
  'seats': [
    {'index': 0, 'player': {'id': _me, 'username': 'me'}},
    {'index': 1, 'player': {'id': _them, 'username': 'them'}},
  ],
  'host': {'id': _me, 'username': 'me'},
  'game': {'moves': moves, 'currentSeat': currentSeat},
});

class _Adapter implements HttpClientAdapter {
  _Adapter(this.room);

  String room;

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

class _Hub extends MatchHub {
  _Hub() : super(serverAddress: 'http://example.invalid', accessToken: _empty);

  static String _empty() => '';

  final pushes = ValueNotifier<RoomSnapshot?>(null);
  final dissolves = ValueNotifier<int>(0);
  final urges = ValueNotifier<int>(0);
  final incoming = ValueNotifier<Map<String, dynamic>?>(null);

  @override
  ValueListenable<RoomSnapshot?> get state => pushes;
  @override
  ValueListenable<int> get dissolved => dissolves;
  @override
  ValueListenable<int> get urged => urges;
  @override
  ValueListenable<Map<String, dynamic>?> get chat => incoming;
  @override
  Future<void> joinRoom(String roomId) async {}
  @override
  Future<void> leaveRoom(String roomId) async {}
  @override
  Future<void> makeMove(String roomId, int row, int col) async {}

  void push(String json) =>
      pushes.value = RoomSnapshot(jsonDecode(json) as Map<String, dynamic>);
}

Future<({GameViewModel vm, _Hub hub})> pumpBoard(
  WidgetTester tester,
  Translations strings, {
  String gameKey = 'gomoku',
}) async {
  final hub = _Hub();
  final dio = buildDio(
    baseUrl: 'http://example.invalid',
    tokens: MemoryTokenStore(),
    refresh: () async => false,
    adapter: _Adapter(roomJson(gameKey: gameKey)),
  );

  // **`runAsync`, because Dio arms a real `Timer` for its send timeout.** Inside
  // `testWidgets`'s fake clock that future never completes, and the symptom is not an
  // error — it is every test in the file reporting "did not complete". Measured here,
  // and it is the second time this trap has cost a debugging round in this repo.
  final catalog = GameCatalogRepository(dio);
  await tester.runAsync(catalog.load);

  final auth = AuthRepository(dio: dio, tokens: MemoryTokenStore())
    ..currentUser = const AuthUser(id: _me, username: 'me');

  final vm = GameViewModel(
    rooms: RoomRepository(dio: dio, hub: hub),
    catalog: catalog,
    auth: auth,
    sound: SoundRepository(
      player: RecordingSoundPlayer(),
      settings: SettingsRepository(MemoryPreferencesStore()),
    ),
    roomId: 'r1',
  );

  // The room is set directly rather than awaited through `open()`: that call also goes
  // through Dio and would hang for the same reason, and what is under test is the
  // layout, not the fetch.
  vm.room = Room.fromJson(jsonDecode(roomJson(gameKey: gameKey)) as Map<String, dynamic>);

  await tester.pumpWidget(
    MultiProvider(
      providers: [
        Provider<Translations>.value(value: strings),
        ChangeNotifierProvider<GameViewModel>.value(value: vm),
      ],
      // **The real theme, not `MaterialApp`'s default.** `_board` reads the board
      // colour off a `BoardColors` theme extension that only `AppTheme.build` attaches
      // (#188) — with a default theme the `!` throws during build and the board simply
      // is not there, which reads as "the screen is broken" rather than "the fixture
      // is wrong".
      child: MaterialApp(
        theme: AppTheme.build(defaultThemeName, Brightness.dark),
        home: const GameView(),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return (vm: vm, hub: hub);
}

void main() {
  late Translations zh;

  setUpAll(() async {
    TestWidgetsFlutterBinding.ensureInitialized();
    zh = await Translations.load(rootBundle, 'zh-CN');
  });

  for (final game in const ['gomoku', 'xiangqi']) {
    testWidgets('$game: the board does not move when the turn changes', (tester) async {
      // **The reported defect, as a measurement.** Nothing about this is game-specific,
      // which is why both are walked: a fix that happened to work for one renderer and
      // not the other would be fixing the wrong thing.
      final o = await pumpBoard(tester, zh, gameKey: game);
      expect(find.byType(GameBoard), findsOneWidget, reason: 'precondition — a board');
      expect(o.vm.mySeat, 0, reason: 'precondition — we are seat 0');
      expect(
        o.vm.urgeDisabledReasonKey,
        'game.urge.button-disabled-own-turn',
        reason: 'precondition — on our turn there IS a reason to show',
      );

      final ourTurn = tester.getRect(find.byType(GameBoard));

      // One ply: the turn passes to the opponent, so the reason disappears.
      o.hub.push(roomJson(
        gameKey: game,
        currentSeat: 1,
        moves: const [{'row': 7, 'col': 7, 'seat': 0}],
      ));
      await tester.pumpAndSettle();

      expect(
        o.vm.urgeDisabledReasonKey,
        isNull,
        reason: 'precondition — and now there is not, which is the whole mechanism',
      );

      final theirTurn = tester.getRect(find.byType(GameBoard));
      expect(
        theirTurn,
        ourTurn,
        reason: 'the board changed when the turn did: $ourTurn -> $theirTurn '
            '(${(theirTurn.width - ourTurn.width).abs()} px wider, '
            '${(theirTurn.top - ourTurn.top).abs()} px lower) — that is the flicker. '
            'Reporting only the vertical shift said "0.0 px" while the box had grown '
            'by 20, which is a message wrong in the shape of an answer.',
      );

      o.vm.dispose();
    });
  }

  testWidgets('nor when a move is refused and the error appears', (tester) async {
    // The same class of defect one row up: an error line above the board also changes
    // the column's height. It fires on a rejected move rather than on every ply, so it
    // is rarer — and rarer is what let the first one hide.
    final o = await pumpBoard(tester, zh);
    final before = tester.getRect(find.byType(GameBoard));

    o.vm.errorKey = 'game.errors.invalid-move';
    o.vm.notifyListeners();
    await tester.pumpAndSettle();

    expect(find.text(zh.t('game.errors.invalid-move')), findsOneWidget,
        reason: 'precondition — the error really is on screen');
    expect(tester.getRect(find.byType(GameBoard)), before);

    o.vm.dispose();
  });

  testWidgets('and the reason is still shown when the button is disabled', (tester) async {
    // **The rule the fix must not break.** `add-mobile-game-actions` requires the urge
    // entry to say *why* it cannot be pressed; reserving the space must not turn into
    // deleting the explanation.
    final o = await pumpBoard(tester, zh);
    expect(
      find.text(zh.t('game.urge.button-disabled-own-turn')),
      findsOneWidget,
      reason: 'a greyed-out control with no explanation is not an explanation',
    );
    o.vm.dispose();
  });
}
