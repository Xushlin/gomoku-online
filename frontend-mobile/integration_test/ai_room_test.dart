// Playing the machine, judged by what appears on screen.
//
//   flutter test integration_test/ai_room_test.dart -d windows \
//     --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199
//
// **The headline test takes no input at all after the room is made.** Choosing to play
// White puts the AI on seat 0, and — measured against the running server — the AI does
// **not** move inside the creation response (`moves: 0`) but has moved seconds later.
// Its first move therefore reaches the client as a **hub push**, which makes
// `fix-mobile-hub-inbound` a precondition: before that, picking White showed a board
// that stayed empty forever, and it would have read as "the bot is broken".
//
// So the criterion is the **screen**, not the server. Asking the server whether it has
// the move only proves the server moved — that distinction cost this project a release
// of the mobile client.
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:provider/provider.dart';

import 'package:gewu_mobile/app.dart';
import 'package:gewu_mobile/data/services/token_store.dart';
import 'package:gewu_mobile/ui/game/board_registry.dart';
import 'package:gewu_mobile/ui/game/view/game_board.dart';
import 'package:gewu_mobile/ui/game/view_model/game_view_model.dart';
import 'package:gewu_mobile/ui/login/view/login_view.dart';

const server = String.fromEnvironment('GEWU_PROBE_SERVER');

Future<({MemoryTokenStore tokens, AppDependencies deps})> _signIn(
  WidgetTester tester,
  String prefix,
) async {
  final stamp = DateTime.now().millisecondsSinceEpoch.toString().substring(7);
  final me = '$prefix$stamp'.padRight(20, 'x').substring(0, 20);

  final tokens = MemoryTokenStore();
  final deps = await AppDependencies.build(rootBundle, baseUrl: server, tokenStore: tokens);

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
  return (tokens: tokens, deps: deps);
}

Future<void> _openLobby(WidgetTester tester, AppDependencies deps, String gameKey) async {
  await tester.tap(find.text(deps.strings.t('games.$gameKey.title')));
  await tester.pumpAndSettle(const Duration(seconds: 4));
}

/// Runs the AI dialog: pick a difficulty and a side, then start.
Future<void> _startAiGame(
  WidgetTester tester,
  AppDependencies deps, {
  required String difficultyKey,
  required String sideKey,
}) async {
  await tester.tap(find.text(deps.strings.t('lobby.ai-game.button')));
  await tester.pumpAndSettle();

  // **Scoped to the dialog, because two keys carry the same words.**
  // `lobby.ai-game.button` and `lobby.ai-game.dialog-title` are both 「新建 AI 对局」,
  // so a bare `find.text` matches the FAB behind the barrier as well and reports two.
  // That is not a defect in the screen — it is a finder that cannot tell them apart.
  final dialog = find.byType(AlertDialog);
  Finder inDialog(String key) =>
      find.descendant(of: dialog, matching: find.text(deps.strings.t(key)));

  expect(dialog, findsOneWidget, reason: 'the dialog must open');
  expect(inDialog('lobby.ai-game.dialog-title'), findsOneWidget);

  await tester.tap(inDialog(difficultyKey));
  await tester.pumpAndSettle();
  await tester.tap(inDialog(sideKey));
  await tester.pumpAndSettle();
  await tester.tap(inDialog('lobby.ai-game.submit'));
  await tester.pumpAndSettle(const Duration(seconds: 8));
  expect(dialog, findsNothing, reason: 'and close again');
}

/// The view model the board on screen is rendering from.
GameViewModel _boardState(WidgetTester tester) =>
    Provider.of<GameViewModel>(tester.element(find.byType(GameBoard)), listen: false);

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  if (server.isEmpty) {
    test(
      'playing the machine needs a live backend',
      () {},
      skip: 'set --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199; it is NOT set now',
    );
    return;
  }

  testWidgets('一字棋 offers only the machine; 五子棋 offers both', (tester) async {
    final signedIn = await _signIn(tester, 'ta');
    final t = signedIn.deps.strings;

    await _openLobby(tester, signedIn.deps, tictactoeGameKey);
    expect(
      find.text(t.t('lobby.game-lobby.unavailable.ai-only-title')),
      findsOneWidget,
      reason: 'the server answers 400 to a human-vs-human room for 一字棋',
    );
    expect(find.text(t.t('lobby.rooms.create-button')), findsNothing);
    expect(find.text(t.t('lobby.ai-game.button')), findsOneWidget);

    // The other direction, in the same run: without it, a lobby that always hides
    // create-room passes the assertions above and breaks the game that shipped first.
    await tester.pageBack();
    await tester.pumpAndSettle(const Duration(seconds: 3));
    await _openLobby(tester, signedIn.deps, gomokuGameKey);
    expect(find.text(t.t('lobby.rooms.create-button')), findsOneWidget);
    expect(find.text(t.t('lobby.ai-game.button')), findsOneWidget);
    expect(find.text(t.t('lobby.game-lobby.unavailable.ai-only-title')), findsNothing);
  });

  testWidgets('playing White: the AI moves first, and it appears without any input',
      (tester) async {
    final signedIn = await _signIn(tester, 'tw');
    await _openLobby(tester, signedIn.deps, tictactoeGameKey);
    await _startAiGame(
      tester,
      signedIn.deps,
      difficultyKey: 'lobby.ai-game.difficulty-hard',
      sideKey: 'lobby.ai-game.side-white',
    );

    expect(find.byType(GameBoard), findsOneWidget, reason: 'the room should be open');

    // **Nothing is tapped from here on.** The AI is on seat 0 and moves on its own;
    // its move can only reach this screen as a hub push.
    var moves = _boardState(tester).moves.length;
    for (var waited = 0; waited < 20 && moves == 0; waited++) {
      await tester.pumpAndSettle(const Duration(seconds: 1));
      moves = _boardState(tester).moves.length;
    }

    expect(
      moves,
      1,
      reason: 'the AI holds seat 0 and moves by itself; this is the inbound half',
    );
    expect(_boardState(tester).moves.single.seat, 0, reason: 'the AI is seat 0');

    // And the turn is now mine, said in words on the screen.
    expect(
      find.text(signedIn.deps.strings.t(
        'game.turn.side-turn',
        {'side': signedIn.deps.strings.t('game.seat.white')},
      )),
      findsOneWidget,
      reason: 'white — me — to play',
    );
  });

  testWidgets('playing Black: it is my turn and the board is empty', (tester) async {
    // The other direction. Without it, an implementation that always waits for the AI
    // would hang forever here, and the test above would not notice.
    final signedIn = await _signIn(tester, 'tb');
    await _openLobby(tester, signedIn.deps, tictactoeGameKey);
    await _startAiGame(
      tester,
      signedIn.deps,
      difficultyKey: 'lobby.ai-game.difficulty-easy',
      sideKey: 'lobby.ai-game.side-black',
    );

    expect(find.byType(GameBoard), findsOneWidget);
    expect(_boardState(tester).moves, isEmpty, reason: 'nobody has moved');
    expect(
      find.text(signedIn.deps.strings.t(
        'game.turn.side-turn',
        {'side': signedIn.deps.strings.t('game.seat.black')},
      )),
      findsOneWidget,
      reason: 'black — me — to play',
    );

    // And a real move goes through: 一字棋 is 五子棋 at three roads, one tap per move.
    final board = tester.getRect(find.byType(GameBoard));
    await tester.tapAt(board.center);
    await tester.pumpAndSettle(const Duration(seconds: 8));

    // Mine, then the AI's answer — both of them on this screen.
    expect(
      _boardState(tester).moves.length,
      greaterThanOrEqualTo(2),
      reason: 'my move and the AI\'s reply',
    );
  });
}
