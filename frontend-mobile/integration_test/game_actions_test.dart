// Resign and urge, driven through the real screen against a real server.
//
//   flutter test integration_test/game_actions_test.dart -d windows \
//     --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199
//
// **Two players, and only one of them can be a widget tree.** The opponent is driven
// over raw REST + SignalR, the same way `test/room_social_probe_test.dart` does it —
// which is also what makes the urge assertion meaningful: the push has to cross a real
// hub, be addressed to a real user id, and land on a screen.
//
// That addressing is exactly what was broken. Before `fix-urge-user-routing` this test
// would have failed with the button working perfectly: `Clients.User(...)` had no
// `IUserIdProvider`, so every directed push in the platform's life went nowhere,
// silently, and no test anywhere went red.
import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:integration_test/integration_test.dart';
import 'package:signalr_netcore/hub_connection_builder.dart';

import 'package:gewu_mobile/app.dart';
import 'package:gewu_mobile/data/services/preferences_store.dart';
import 'package:gewu_mobile/data/services/token_store.dart';
import 'package:gewu_mobile/i18n/translations.dart';
import 'package:gewu_mobile/ui/game/board_registry.dart';
import 'package:gewu_mobile/ui/game/view/game_board.dart';
import 'package:gewu_mobile/ui/login/view/login_view.dart';

const server = String.fromEnvironment('GEWU_PROBE_SERVER');

Future<Map<String, dynamic>> _post(String path, Object body, {String? token}) async {
  final res = await http.post(
    Uri.parse('$server$path'),
    headers: {
      'content-type': 'application/json',
      if (token != null) 'authorization': 'Bearer $token',
    },
    body: jsonEncode(body),
  );
  if (res.statusCode >= 400) throw StateError('$path -> ${res.statusCode} ${res.body}');
  return res.body.isEmpty ? <String, dynamic>{} : jsonDecode(res.body) as Map<String, dynamic>;
}

/// The opponent, played by a script.
Future<Map<String, dynamic>> _register(String prefix, String stamp) => _post(
  '/api/auth/register',
  {
    'email': '$prefix$stamp@example.com',
    'username': '$prefix$stamp',
    'password': 'Probe-pass-1234',
  },
);

/// Signs in through the real form and lands on the catalogue.
Future<AppDependencies> _signIn(WidgetTester tester, String prefix, String stamp) async {
  final me = '$prefix$stamp'.padRight(20, 'x').substring(0, 20);
  final deps = await AppDependencies.build(
    rootBundle,
    baseUrl: server,
    tokenStore: MemoryTokenStore(),
    preferences: MemoryPreferencesStore(),
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
  expect(find.byType(LoginView), findsNothing, reason: 'registration must have worked');
  return deps;
}

Finder createRoomButton(Translations t) => find.text(t.t('lobby.rooms.create-button'));

/// Opens 五子棋's lobby and creates a room, ending on the board.
Future<void> _openARoom(WidgetTester tester, Translations t) async {
  await tester.tap(find.text(t.t('games.$gomokuGameKey.title')));
  await tester.pumpAndSettle(const Duration(seconds: 4));
  await tester.tap(createRoomButton(t));
  await tester.pumpAndSettle(const Duration(seconds: 6));
  expect(find.byType(GameBoard), findsOneWidget, reason: 'the room should be open');
}

/// The newest waiting room hosted by [username].
///
/// **Filtered by host, with no fallback.** An earlier test here took "the first waiting
/// room" and asserted against a room left over from a curl experiment; it went red,
/// which was lucky — the same shortcut is normally green for the wrong reason.
Future<String> _roomHostedBy(String username, String token) async {
  final res = await http.get(
    Uri.parse('$server/api/rooms?gameKey=$gomokuGameKey'),
    headers: {'authorization': 'Bearer $token'},
  );
  final rooms = (jsonDecode(res.body) as List<dynamic>).cast<Map<String, dynamic>>();
  final mine = rooms.where((r) => '${(r['host'] as Map?)?['username']}' == username);
  expect(mine, isNotEmpty, reason: 'the room we just created must be listed');
  return '${mine.last['id']}';
}

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  if (server.isEmpty) {
    test(
      'resign and urge need a live backend and a second player',
      () {},
      skip: 'set --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199; it is NOT set now',
    );
    return;
  }

  testWidgets('being urged shows up on screen without a refresh', (tester) async {
    final stamp = DateTime.now().millisecondsSinceEpoch.toString().substring(7);
    final deps = await _signIn(tester, 'ua', stamp);
    final t = deps.strings;
    final me = 'ua$stamp'.padRight(20, 'x').substring(0, 20);

    await _openARoom(tester, t);

    // The opponent joins for real, which is what starts the game.
    final them = await _register('ub', stamp);
    final roomId = await _roomHostedBy(me, deps.tokens.access!);
    await _post('/api/rooms/$roomId/join', {}, token: them['accessToken'] as String);
    await tester.pumpAndSettle(const Duration(seconds: 4));

    // We are seat 0 and move first, so we are the one holding things up — which is
    // precisely who `Room.UrgeOpponent` urges.
    expect(find.text(t.t('game.urge.toast')), findsNothing, reason: 'precondition');

    final connection = HubConnectionBuilder()
        .withUrl('$server/hubs/match?access_token='
            '${Uri.encodeComponent(them['accessToken'] as String)}')
        .build();
    await connection.start();
    await connection.invoke('JoinRoom', args: [roomId]);
    await connection.invoke('Urge', args: [roomId]);

    // A bounded wait, pumping — the SnackBar is scheduled after a frame.
    var seen = false;
    for (var i = 0; i < 40 && !seen; i++) {
      await tester.pump(const Duration(milliseconds: 250));
      seen = find.text(t.t('game.urge.toast')).evaluate().isNotEmpty;
    }
    await connection.stop();

    expect(
      seen,
      isTrue,
      reason: 'the urge must reach the screen — this is the assertion that '
          '`Clients.User(...)` with no IUserIdProvider used to fail silently',
    );
  });

  testWidgets('resigning asks first, and the result comes from the server', (tester) async {
    final stamp = DateTime.now().millisecondsSinceEpoch.toString().substring(7);
    final deps = await _signIn(tester, 'ra', stamp);
    final t = deps.strings;
    final me = 'ra$stamp'.padRight(20, 'x').substring(0, 20);

    await _openARoom(tester, t);

    // Waiting rooms cannot be resigned — assert the button is absent BEFORE the
    // opponent arrives, or "it appeared" proves nothing about when.
    expect(
      find.text(t.t('game.actions.resign')),
      findsNothing,
      reason: 'a waiting room has nothing to resign',
    );

    final them = await _register('rb', stamp);
    final roomId = await _roomHostedBy(me, deps.tokens.access!);
    await _post('/api/rooms/$roomId/join', {}, token: them['accessToken'] as String);
    await tester.pumpAndSettle(const Duration(seconds: 4));

    expect(find.text(t.t('game.actions.resign')), findsOneWidget, reason: 'now it is there');

    // --- cancel leaves the game alone ---------------------------------------
    await tester.tap(find.text(t.t('game.actions.resign')));
    await tester.pumpAndSettle();
    expect(find.byType(AlertDialog), findsOneWidget, reason: 'it must ask');
    await tester.tap(
      find.widgetWithText(TextButton, t.t('game.actions.resign-confirm-cancel')),
    );
    await tester.pumpAndSettle(const Duration(seconds: 2));

    final afterCancel = jsonDecode((await http.get(
      Uri.parse('$server/api/rooms/$roomId'),
      headers: {'authorization': 'Bearer ${deps.tokens.access}'},
    )).body) as Map<String, dynamic>;
    expect(afterCancel['status'], 'Playing', reason: 'cancel must not resign');

    // --- confirm ends it, and the SERVER is what says so ---------------------
    await tester.tap(find.text(t.t('game.actions.resign')));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(TextButton, t.t('game.actions.resign-confirm-ok')));
    await tester.pumpAndSettle(const Duration(seconds: 5));

    final ended = jsonDecode((await http.get(
      Uri.parse('$server/api/rooms/$roomId'),
      headers: {'authorization': 'Bearer ${deps.tokens.access}'},
    )).body) as Map<String, dynamic>;
    expect(ended['status'], 'Finished');
    expect((ended['game'] as Map<String, dynamic>)['endReason'], 'Resigned');
    expect(
      (ended['game'] as Map<String, dynamic>)['winnerUserId'],
      (them['user'] as Map<String, dynamic>)['id'],
      reason: 'resigning gives the win to the other seat',
    );

    // And the screen says it — through the existing outcome path, not through anything
    // `resign()` wrote down.
    expect(find.text(t.t('game.ended.title-lose')), findsOneWidget);
    expect(find.text(t.t('game.ended.reason-resigned')), findsOneWidget);
  });
}
