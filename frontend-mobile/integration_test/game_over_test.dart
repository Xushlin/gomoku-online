// A game played to the end, and the result said on screen.
//
//   flutter test integration_test/game_over_test.dart -d windows \
//     --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199
//
// **The defect this covers was found on a real phone: a finished game just stopped.**
// The board stayed, every tap was refused by the server, and nothing said who won.
//
// The criterion is the **screen**. Asking the server whether the game is over only
// proves the server ended it — and that distinction has now cost this project twice
// (`fix-mobile-hub-inbound`, then the AI's first move).
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:provider/provider.dart';

import 'package:gewu_mobile/app.dart';
import 'package:gewu_mobile/data/services/token_store.dart';
import 'package:gewu_mobile/ui/game/board_registry.dart';
import 'package:gewu_mobile/ui/game/view/board_geometry.dart';
import 'package:gewu_mobile/ui/game/view/game_board.dart';
import 'package:gewu_mobile/ui/game/view_model/game_view_model.dart';
import 'package:gewu_mobile/ui/login/view/login_view.dart';

const server = String.fromEnvironment('GEWU_PROBE_SERVER');

Future<AppDependencies> _signIn(WidgetTester tester, String prefix) async {
  final stamp = DateTime.now().millisecondsSinceEpoch.toString().substring(7);
  final me = '$prefix$stamp'.padRight(20, 'x').substring(0, 20);

  final deps = await AppDependencies.build(
    rootBundle,
    baseUrl: server,
    tokenStore: MemoryTokenStore(),
  );
  await tester.pumpWidget(GewuApp(deps: deps));
  await tester.pumpAndSettle();
  await tester.tap(find.text(deps.strings.t('auth.login.no-account-cta')));
  await tester.pumpAndSettle();

  final fields = find.byType(TextField);
  await tester.enterText(fields.at(0), '$me@example.com');
  await tester.enterText(fields.at(1), me);
  await tester.enterText(fields.at(2), 'Mobile-pass-1234');
  await tester.pumpAndSettle();
  await tester.tap(find.text(deps.strings.t('auth.register.submit')));
  await tester.pumpAndSettle(const Duration(seconds: 6));

  expect(find.byType(LoginView), findsNothing);
  return deps;
}

GameViewModel _board(WidgetTester tester) =>
    Provider.of<GameViewModel>(tester.element(find.byType(GameBoard)), listen: false);

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  if (server.isEmpty) {
    test(
      'a finished game needs a live backend',
      () {},
      skip: 'set --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199; it is NOT set now',
    );
    return;
  }

  testWidgets('一字棋 played to the end says who won, once', (tester) async {
    final deps = await _signIn(tester, 'go');
    final t = deps.strings;

    // 一字棋 on Easy: nine cells, so a decided result arrives in a handful of moves.
    await tester.tap(find.text(t.t('games.$tictactoeGameKey.title')));
    await tester.pumpAndSettle(const Duration(seconds: 4));
    await tester.tap(find.text(t.t('lobby.ai-game.button')));
    await tester.pumpAndSettle();

    final dialog = find.byType(AlertDialog);
    Finder inDialog(String key) =>
        find.descendant(of: dialog, matching: find.text(t.t(key)));
    await tester.tap(inDialog('lobby.ai-game.difficulty-easy'));
    await tester.pumpAndSettle();
    await tester.tap(inDialog('lobby.ai-game.side-black'));
    await tester.pumpAndSettle();
    await tester.tap(inDialog('lobby.ai-game.submit'));
    await tester.pumpAndSettle(const Duration(seconds: 8));

    expect(find.byType(GameBoard), findsOneWidget, reason: 'the room should be open');
    expect(_board(tester).outcome, isNull, reason: 'precondition — nobody has won yet');

    // --- play until it is over ----------------------------------------------
    final rect = tester.getRect(find.byType(GameBoard));
    final geometry = BoardGeometry.fit(
      rows: 3,
      cols: 3,
      canvas: Size(rect.width, rect.height),
    );

    for (var attempt = 0; attempt < 12 && _board(tester).outcome == null; attempt++) {
      final taken = {
        for (final m in _board(tester).moves) '${m.row},${m.col}',
      };
      final free = [
        for (var row = 0; row < 3; row++)
          for (var col = 0; col < 3; col++)
            if (!taken.contains('$row,$col')) (row, col),
      ];
      if (free.isEmpty) break;

      await tester.tapAt(rect.topLeft + geometry.centreOf(free.first.$1, free.first.$2));
      // Long enough for my move, the AI's reply and the pushes for both.
      await tester.pumpAndSettle(const Duration(seconds: 4));
    }

    // --- the result, on screen ----------------------------------------------
    final outcome = _board(tester).outcome;
    expect(outcome, isNotNull, reason: 'nine cells cannot stay undecided');

    final titles = [
      t.t('game.ended.title-win'),
      t.t('game.ended.title-lose'),
      t.t('game.ended.title-draw'),
    ];
    expect(
      titles,
      contains(t.t(outcome!.titleKey)),
      reason: 'the title must be one of the three, not a raw key',
    );
    expect(
      find.text(t.t(outcome.titleKey)),
      findsOneWidget,
      reason: 'and it must be ON SCREEN — the whole defect was a screen that said nothing',
    );
    expect(find.text(t.t('game.ended.back-to-lobby')), findsOneWidget);

    // --- dismissed once, and it stays dismissed ------------------------------
    await tester.tap(find.text(t.t('game.ended.dismiss')));
    await tester.pumpAndSettle(const Duration(seconds: 3));
    expect(find.byType(AlertDialog), findsNothing, reason: 'closing must close it');
    expect(find.byType(GameBoard), findsOneWidget, reason: 'and leave the board visible');

    // A later push must not re-open it. Tapping the board produces one (the server
    // refuses the move on a finished game), which is exactly the situation that would
    // re-announce.
    await tester.tapAt(rect.topLeft + geometry.centreOf(1, 1));
    await tester.pumpAndSettle(const Duration(seconds: 4));
    expect(
      find.byType(AlertDialog),
      findsNothing,
      reason: 're-announcing on every later push is worse than not announcing at all',
    );
  });
}
