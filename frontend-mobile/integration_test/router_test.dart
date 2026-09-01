// The route table, driven through the real widget tree against a real server.
//
//   flutter test integration_test/router_test.dart -d windows \
//     --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199
//
// These four readings are the ones that were taken BEFORE the route table existed and
// came back wrong:
//
//   canPop-in-game=false        popRoute-left-the-room=false
//   on-screen-arrow-works=true  dead-session-lands-at-login=false
//
// The third is why the finding was "the system back button does nothing" rather than
// "you cannot leave a room", and it is kept below as a control for the same reason.
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:integration_test/integration_test.dart';

import 'package:gewu_mobile/app.dart';
import 'package:gewu_mobile/data/services/token_store.dart';
import 'package:gewu_mobile/ui/game/view/gomoku_board.dart';
import 'package:gewu_mobile/ui/login/view/login_view.dart';

const server = String.fromEnvironment('GEWU_PROBE_SERVER');

Future<MemoryTokenStore> _registerAndReachLobby(WidgetTester tester, String prefix) async {
  final stamp = DateTime.now().millisecondsSinceEpoch.toString().substring(7);
  // 20 characters: the registration cap, i.e. the longest real content. Short names
  // pass every layout assertion, which is the trap this repo keeps re-learning.
  final me = '$prefix$stamp'.padRight(20, 'x').substring(0, 20);

  final tokens = MemoryTokenStore();
  final deps = await AppDependencies.build(rootBundle, baseUrl: server, tokenStore: tokens);

  await tester.pumpWidget(GewuApp(deps: deps));
  await tester.pumpAndSettle();

  // Signed out, and the initial location is the lobby — so the redirect must already
  // have moved us. If this fails, the redirect never ran.
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

  // The other direction: signed in AT /login must bounce to the lobby. Nothing in
  // LoginView navigates any more, so this is the redirect and only the redirect.
  expect(find.byType(FloatingActionButton), findsOneWidget, reason: 'redirect: signed in -> /');
  expect(find.byType(LoginView), findsNothing);
  return tokens;
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

  testWidgets('a room is a route the system back button can pop', (tester) async {
    await _registerAndReachLobby(tester, 'rt');

    // --- direction one: the lobby is the bottom of the stack -----------------
    // Without this half, an implementation that pops anything at all would pass the
    // assertions below while also throwing a logged-in person back to the login page.
    expect(
      _canPopUnder(tester, find.byType(FloatingActionButton)),
      isFalse,
      reason: 'the lobby must be the bottom of the stack',
    );

    // --- direction two: a room sits on top of it -----------------------------
    await tester.tap(find.byType(FloatingActionButton));
    await tester.pumpAndSettle(const Duration(seconds: 6));
    expect(find.byType(GomokuBoard), findsOneWidget, reason: 'the room should be open');

    expect(
      _canPopUnder(tester, find.byType(GomokuBoard)),
      isTrue,
      reason: 'measured false before the route table: rooms/:id must nest under /',
    );

    await systemBack(tester);
    expect(find.byType(GomokuBoard), findsNothing, reason: 'system back must leave the room');
    expect(find.byType(FloatingActionButton), findsOneWidget, reason: 'and land on the lobby');
  });

  testWidgets('the on-screen arrow is the same mechanism, not a second one', (tester) async {
    // The control. If this failed, the finding would be "you cannot leave a room",
    // which is a different claim — and it is what kept the earlier conclusion honest.
    // It now shares the navigator with the system back button because `AppBar` shows
    // its own leading button exactly when `canPop()` is true.
    await _registerAndReachLobby(tester, 'ra');
    await tester.tap(find.byType(FloatingActionButton));
    await tester.pumpAndSettle(const Duration(seconds: 6));
    expect(find.byType(GomokuBoard), findsOneWidget);

    expect(find.byIcon(Icons.arrow_back), findsOneWidget, reason: 'AppBar draws it itself now');
    await tester.tap(find.byIcon(Icons.arrow_back));
    await tester.pumpAndSettle(const Duration(seconds: 3));
    expect(find.byType(GomokuBoard), findsNothing);
    expect(find.byType(FloatingActionButton), findsOneWidget);
  });

  testWidgets('a dead session lands at the login page', (tester) async {
    final tokens = await _registerAndReachLobby(tester, 'rd');

    // **The precondition, and it is the whole test.** A negative-turned-positive
    // assertion ("we ended at login") is green for the wrong reason if the session
    // was never alive — e.g. registration silently failed and we were at /login all
    // along. So: prove it works first.
    final alive = await http.get(
      Uri.parse('$server/api/rooms?gameKey=gomoku'),
      headers: {'authorization': 'Bearer ${tokens.access}'},
    );
    expect(alive.statusCode, 200, reason: 'the session must be alive before we kill it');
    expect(find.byType(LoginView), findsNothing, reason: 'and we must not already be at login');

    // Kill it the way an expiry does: both tokens unusable, so the interceptor's
    // refresh fails too.
    tokens.access = 'not-a-token';
    await tokens.writeRefresh('not-a-refresh-token');

    await tester.tap(find.byIcon(Icons.refresh));
    await tester.pumpAndSettle(const Duration(seconds: 6));

    // Measured false before this change: you stayed on the lobby with an error toast.
    expect(find.byType(LoginView), findsOneWidget, reason: 'a dead session must reach /login');
  });
}
