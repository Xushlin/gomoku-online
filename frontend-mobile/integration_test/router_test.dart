// The route table, driven through the real widget tree against a real server.
//
//   flutter test integration_test/router_test.dart -d windows \
//     --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199
//
// Four readings were taken here BEFORE the route table existed and came back wrong:
//
//   canPop-in-game=false        popRoute-left-the-room=false
//   on-screen-arrow-works=true  dead-session-lands-at-login=false
//
// The third is why the finding was "the system back button does nothing" rather than
// "you cannot leave a room", and it is kept below as a control for the same reason.
//
// The stack is now three deep — catalogue, lobby, game — so `canPop` is asserted at
// every level, including the bottom, where it must be **false**.
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:integration_test/integration_test.dart';

import 'package:gewu_mobile/app.dart';
import 'package:gewu_mobile/i18n/translations.dart';
import 'package:gewu_mobile/data/services/token_store.dart';
import 'package:gewu_mobile/ui/game/board_registry.dart';
import 'package:gewu_mobile/ui/game/view/game_board.dart';
import 'package:gewu_mobile/ui/login/view/login_view.dart';

const server = String.fromEnvironment('GEWU_PROBE_SERVER');

/// Registers through the real form and ends on the catalogue.
///
/// Asserts **both** redirect directions on the way: signed out lands at `/login` even
/// though the initial location is `/`, and signing in bounces off `/login` — nothing in
/// `LoginView` navigates any more, so the second one is the redirect and only the
/// redirect.
Future<({MemoryTokenStore tokens, AppDependencies deps})> _signIn(
  WidgetTester tester,
  String prefix,
) async {
  final stamp = DateTime.now().millisecondsSinceEpoch.toString().substring(7);
  // 20 characters: the registration cap, i.e. the longest real content. Short names
  // pass every layout assertion, which is the trap this repo keeps re-learning.
  final me = '$prefix$stamp'.padRight(20, 'x').substring(0, 20);

  final tokens = MemoryTokenStore();
  final deps = await AppDependencies.build(rootBundle, baseUrl: server, tokenStore: tokens);

  await tester.pumpWidget(GewuApp(deps: deps));
  await tester.pumpAndSettle();

  expect(find.byType(LoginView), findsOneWidget, reason: 'redirect: signed out -> /login');

  await tester.tap(find.text(deps.strings.t('auth.login.no-account-cta')));
  await tester.pumpAndSettle();

  final fields = find.byType(TextField);
  expect(fields, findsNWidgets(3), reason: 'email + username + password');
  await tester.enterText(fields.at(0), '$me@example.com');
  await tester.enterText(fields.at(1), me);
  await tester.enterText(fields.at(2), 'Mobile-pass-1234');
  await tester.pumpAndSettle();
  await tester.tap(find.text(deps.strings.t('auth.register.submit')));
  await tester.pumpAndSettle(const Duration(seconds: 6));

  expect(
    find.text(deps.strings.t('catalog.title')),
    findsOneWidget,
    reason: 'redirect: signed in -> / (the catalogue)',
  );
  expect(find.byType(LoginView), findsNothing);
  return (tokens: tokens, deps: deps);
}

/// Catalogue -> 五子棋's lobby.
Future<void> _enterGomokuLobby(WidgetTester tester, AppDependencies deps) async {
  final card = find.text(deps.strings.t('games.$gomokuGameKey.title'));
  expect(card, findsOneWidget, reason: 'the catalogue must list 五子棋');
  await tester.tap(card);
  await tester.pumpAndSettle(const Duration(seconds: 4));
  expect(createRoomButton(deps.strings), findsOneWidget, reason: 'lobby should be showing');
}

bool _canPopUnder(WidgetTester tester, Finder anchor) =>
    Navigator.of(tester.element(anchor)).canPop();

/// Exactly what Android's back gesture sends. `WidgetsApp` asks the navigator to
/// `maybePop()`; when that returns false the framework tells the engine it did not
/// handle it and Android finishes the activity — which is what used to happen here.
Future<void> systemBack(WidgetTester tester) async {
  await tester.binding.defaultBinaryMessenger.handlePlatformMessage(
    'flutter/navigation',
    const JSONMethodCodec().encodeMethodCall(const MethodCall('popRoute')),
    (_) {},
  );
  await tester.pumpAndSettle(const Duration(seconds: 2));
}

/// The lobby's **create-room** button, found by its label.
///
/// **`find.byType(FloatingActionButton)` stopped being unambiguous** the day the lobby
/// gained a second one (「新建 AI 对局」). Naming the button is better than counting
/// them anyway: the label is what a player sees, and a finder that says *which* button
/// cannot quietly start tapping the other one.
Finder createRoomButton(Translations strings) =>
    find.text(strings.t('lobby.rooms.create-button'));

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  if (server.isEmpty) {
    test(
      'the route table needs a live backend',
      () {},
      skip: 'set --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199; it is NOT set now',
    );
    return;
  }

  testWidgets('three levels, and the system back button pops each one', (tester) async {
    final signedIn = await _signIn(tester, 'rt');
    final deps = signedIn.deps;

    // --- the catalogue is the bottom of the stack ----------------------------
    // Without this half, an implementation that pops anything at all would pass the
    // assertions below while also throwing a signed-in person back to the login page.
    expect(
      _canPopUnder(tester, find.text(deps.strings.t('catalog.title'))),
      isFalse,
      reason: 'the catalogue must be the bottom of the stack',
    );

    // --- the lobby sits on it ------------------------------------------------
    await _enterGomokuLobby(tester, deps);
    expect(
      _canPopUnder(tester, createRoomButton(deps.strings)),
      isTrue,
      reason: 'games/:key must nest under /',
    );

    // --- and a room sits on the lobby ---------------------------------------
    await tester.tap(createRoomButton(signedIn.deps.strings));
    await tester.pumpAndSettle(const Duration(seconds: 6));
    expect(find.byType(GameBoard), findsOneWidget, reason: 'the room should be open');
    expect(
      _canPopUnder(tester, find.byType(GameBoard)),
      isTrue,
      reason: 'measured false before the route table: rooms/:id must nest under games/:key',
    );

    // --- back down the stack, one level per press ----------------------------
    await systemBack(tester);
    expect(find.byType(GameBoard), findsNothing, reason: 'back must leave the room');
    expect(createRoomButton(deps.strings), findsOneWidget, reason: 'and land on the lobby');

    await systemBack(tester);
    expect(
      find.text(deps.strings.t('catalog.title')),
      findsOneWidget,
      reason: 'back again must land on the catalogue',
    );
    expect(find.byType(LoginView), findsNothing, reason: 'and MUST NOT reach the login page');
  });

  testWidgets('the catalogue lists what the server serves, minus the one with no copy',
      (tester) async {
    final signedIn = await _signIn(tester, 'rc');
    final deps = signedIn.deps;

    // The same three numbers the unit test pins, but against the **live** API rather
    // than a copied fixture — so a server that starts serving an eighth game shows up
    // here even though the fixture would not have moved.
    final served = await http.get(
      Uri.parse('$server/api/games'),
      headers: {'authorization': 'Bearer ${signedIn.tokens.access}'},
    );
    expect(served.statusCode, 200);

    // Every game with copy must be on screen…
    final games = await deps.catalog.load();
    final withCopy = games
        .where((g) => deps.strings.t('games.${g.gameKey}.title') != 'games.${g.gameKey}.title')
        .toList();
    for (final g in withCopy) {
      expect(
        find.text(deps.strings.t('games.${g.gameKey}.title')),
        findsOneWidget,
        reason: g.gameKey,
      );
    }

    // …and both sides of the sample are non-empty, or neither check means anything.
    expect(withCopy.length, lessThan(games.length), reason: 'exactly one game has no copy');
    expect(games.length - withCopy.length, 1);
    expect(
      withCopy.where((g) => boardRenderers.containsKey(g.gameKey)),
      hasLength(boardRenderers.length),
      reason: 'every game this client can draw is listed and enabled',
    );

    // The raw key must not be on screen anywhere — that is what the filter prevents.
    expect(find.text('games.xiangqi-endgame.title'), findsNothing);
  });

  testWidgets('the on-screen arrow is the same mechanism, not a second one', (tester) async {
    // The control. If this failed, the finding would be "you cannot leave a room",
    // which is a different claim — and it is what kept the earlier conclusion honest.
    // It now shares the navigator with the system back button because `AppBar` shows
    // its own leading button exactly when `canPop()` is true.
    final signedIn = await _signIn(tester, 'ra');
    await _enterGomokuLobby(tester, signedIn.deps);
    await tester.tap(createRoomButton(signedIn.deps.strings));
    await tester.pumpAndSettle(const Duration(seconds: 6));
    expect(find.byType(GameBoard), findsOneWidget);

    expect(find.byIcon(Icons.arrow_back), findsOneWidget, reason: 'AppBar draws it itself now');
    await tester.tap(find.byIcon(Icons.arrow_back));
    await tester.pumpAndSettle(const Duration(seconds: 3));
    expect(find.byType(GameBoard), findsNothing);
    expect(createRoomButton(signedIn.deps.strings), findsOneWidget);
  });

  testWidgets('a dead session lands at the login page', (tester) async {
    final signedIn = await _signIn(tester, 'rd');
    final tokens = signedIn.tokens;

    // **The precondition, and it is the whole test.** "We ended at login" is green for
    // the wrong reason if the session was never alive — e.g. registration silently
    // failed and we were at /login all along. So: prove it works first.
    final alive = await http.get(
      Uri.parse('$server/api/rooms?gameKey=$gomokuGameKey'),
      headers: {'authorization': 'Bearer ${tokens.access}'},
    );
    expect(alive.statusCode, 200, reason: 'the session must be alive before we kill it');
    expect(find.byType(LoginView), findsNothing, reason: 'and we must not already be at login');

    await _enterGomokuLobby(tester, signedIn.deps);

    // Kill it the way an expiry does: both tokens unusable, so the interceptor's
    // refresh fails too.
    tokens.access = 'not-a-token';
    await tokens.writeRefresh('not-a-refresh-token');

    await tester.tap(find.byIcon(Icons.refresh));
    await tester.pumpAndSettle(const Duration(seconds: 6));

    // Measured false before the route table: you stayed on the lobby with an error.
    expect(find.byType(LoginView), findsOneWidget, reason: 'a dead session must reach /login');
  });
}
