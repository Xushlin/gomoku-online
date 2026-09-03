// Chat, driven through the real screen against a real server.
//
//   flutter test integration_test/chat_test.dart -d windows \
//     --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199
//
// **The criterion is the screen.** Asking the server whether it stored a message only
// proves the server stored it, and that distinction has cost this project three times
// (`fix-mobile-hub-inbound`, the AI's first move, the game-over dialog).
//
// The opponent is a script — only one player can be a widget tree — which is also what
// makes the inbound assertion real: the message has to cross a live hub, reach the room
// group, and be appended to a list that was seeded from a REST snapshot.
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

Future<Map<String, dynamic>> _register(String prefix, String stamp) => _post(
  '/api/auth/register',
  {
    'email': '$prefix$stamp@example.com',
    'username': '$prefix$stamp',
    'password': 'Probe-pass-1234',
  },
);

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
  expect(find.byType(LoginView), findsNothing);
  return deps;
}

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
      'chat needs a live backend and a second player',
      () {},
      skip: 'set --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199; it is NOT set now',
    );
    return;
  }

  testWidgets('what the opponent says arrives, and what we say is stored', (tester) async {
    final stamp = DateTime.now().millisecondsSinceEpoch.toString().substring(7);
    final deps = await _signIn(tester, 'ca', stamp);
    final t = deps.strings;
    final me = 'ca$stamp'.padRight(20, 'x').substring(0, 20);

    // A room, and an opponent in it.
    await tester.tap(find.text(t.t('games.$gomokuGameKey.title')));
    await tester.pumpAndSettle(const Duration(seconds: 4));
    await tester.tap(find.text(t.t('lobby.rooms.create-button')));
    await tester.pumpAndSettle(const Duration(seconds: 6));
    expect(find.byType(GameBoard), findsOneWidget);

    final them = await _register('cb', stamp);
    final theirToken = them['accessToken'] as String;
    final roomId = await _roomHostedBy(me, deps.tokens.access!);
    await _post('/api/rooms/$roomId/join', {}, token: theirToken);
    await tester.pumpAndSettle(const Duration(seconds: 4));

    // --- the panel opens, and starts empty ----------------------------------
    await tester.tap(find.byTooltip(t.t('game.chat.title')));
    await tester.pumpAndSettle(const Duration(seconds: 2));
    expect(
      find.text(t.t('game.chat.empty')),
      findsOneWidget,
      reason: 'precondition: nothing has been said yet',
    );

    // --- they say something, over a real hub --------------------------------
    final theirs = 'hello-from-them-$stamp';
    final connection = HubConnectionBuilder()
        .withUrl('$server/hubs/match?access_token=${Uri.encodeComponent(theirToken)}')
        .build();
    await connection.start();
    await connection.invoke('JoinRoom', args: [roomId]);
    await connection.invoke('SendChat', args: [roomId, theirs, 'Room']);

    var seen = false;
    for (var i = 0; i < 40 && !seen; i++) {
      await tester.pump(const Duration(milliseconds: 250));
      seen = find.text(theirs).evaluate().isNotEmpty;
    }
    expect(seen, isTrue, reason: 'their message must appear WITHOUT reopening the panel');
    expect(find.text(t.t('game.chat.empty')), findsNothing);

    // --- we say something, through the real field and button ----------------
    final mine = 'hello-from-me-$stamp';
    await tester.enterText(find.byType(TextField).last, mine);
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, t.t('game.chat.send')));
    await tester.pumpAndSettle(const Duration(seconds: 3));

    // On screen…
    expect(find.text(mine), findsOneWidget);
    // …and in the room the server actually has. Both halves: the screen is the
    // criterion, and the server proves it was not only local.
    final snapshot = jsonDecode((await http.get(
      Uri.parse('$server/api/rooms/$roomId'),
      headers: {'authorization': 'Bearer ${deps.tokens.access}'},
    )).body) as Map<String, dynamic>;
    final stored = (snapshot['chatMessages'] as List<dynamic>)
        .cast<Map<String, dynamic>>()
        .map((m) => '${m['content']}')
        .toList();
    expect(stored, containsAll(<String>[theirs, mine]));

    // The history is what a fresh open would seed from — assert the shape it relies on.
    expect(
      (snapshot['chatMessages'] as List<dynamic>).first,
      containsPair('channel', 'Room'),
      reason: 'the channel comes back as a string, which is what the client parses',
    );

    await connection.stop();
  });
}
