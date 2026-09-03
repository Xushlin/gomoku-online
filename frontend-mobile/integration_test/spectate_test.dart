// Watching somebody else's game, driven through the real screen.
//
//   flutter test integration_test/spectate_test.dart -d windows \
//     --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199
//
// **Both players are scripts here** — the widget tree is the spectator, which is the
// only one of the three whose screen is the thing under test.
//
// The sharp assertion is "the spectator hears the table". Room-channel chat goes to the
// *room* group, which `JoinRoom` joins; `JoinSpectatorGroup` only adds the spectator
// sub-group. A version that called only the second produces a silent spectator, and
// that reads exactly like a server bug — `test/room_social_probe_test.dart` made
// precisely that mistake before any of this existed.
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
import 'package:gewu_mobile/ui/game/view/board_geometry.dart';
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

Future<Map<String, dynamic>> _get(String path, String token) async {
  final res = await http.get(
    Uri.parse('$server$path'),
    headers: {'authorization': 'Bearer $token'},
  );
  return jsonDecode(res.body) as Map<String, dynamic>;
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

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  if (server.isEmpty) {
    test(
      'spectating needs a live backend and two other players',
      () {},
      skip: 'set --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199; it is NOT set now',
    );
    return;
  }

  testWidgets('a spectator watches, hears the table, and sends nothing', (tester) async {
    final stamp = DateTime.now().millisecondsSinceEpoch.toString().substring(7);

    // Two players, both scripts. They start a real game before the watcher exists.
    final p1 = await _register('sp1', stamp);
    final p2 = await _register('sp2', stamp);
    final room = await _post(
      '/api/rooms',
      {'name': 'watch-me-$stamp', 'gameKey': gomokuGameKey},
      token: p1['accessToken'] as String,
    );
    final roomId = '${room['id']}';
    await _post('/api/rooms/$roomId/join', {}, token: p2['accessToken'] as String);

    // The watcher.
    final deps = await _signIn(tester, 'sw', stamp);
    final t = deps.strings;

    await tester.tap(find.text(t.t('games.$gomokuGameKey.title')));
    await tester.pumpAndSettle(const Duration(seconds: 5));

    // --- the lobby offers to watch, not to join ------------------------------
    final tile = find.text('watch-me-$stamp');
    expect(tile, findsOneWidget, reason: 'the room must be listed');
    expect(
      find.descendant(of: find.byType(ListTile), matching: find.text(t.t('lobby.rooms.watch'))),
      findsWidgets,
      reason: 'a full room offers watching, not a seat',
    );

    await tester.tap(tile);
    await tester.pumpAndSettle(const Duration(seconds: 6));
    expect(find.byType(GameBoard), findsOneWidget, reason: 'the board must be showing');

    // The server agrees we are watching.
    final seen = await _get('/api/rooms/$roomId', deps.tokens.access!);
    expect(
      (seen['spectators'] as List<dynamic>).map((s) => '${(s as Map)['username']}'),
      contains('sw$stamp'.padRight(20, 'x').substring(0, 20)),
    );

    // --- no player controls ---------------------------------------------------
    expect(find.text(t.t('game.actions.resign')), findsNothing);
    expect(find.text(t.t('game.actions.urge')), findsNothing);

    // --- the spectator hears the table ---------------------------------------
    // **This is what `JoinRoom` buys.** Without that step the message never arrives and
    // it looks like the server dropped it.
    await tester.tap(find.byTooltip(t.t('game.chat.title')));
    await tester.pumpAndSettle(const Duration(seconds: 2));

    final said = 'from-the-table-$stamp';
    final connection = HubConnectionBuilder()
        .withUrl('$server/hubs/match?access_token='
            '${Uri.encodeComponent(p1['accessToken'] as String)}')
        .build();
    await connection.start();
    await connection.invoke('JoinRoom', args: [roomId]);
    await connection.invoke('SendChat', args: [roomId, said, 'Room']);

    var heard = false;
    for (var i = 0; i < 40 && !heard; i++) {
      await tester.pump(const Duration(milliseconds: 250));
      heard = find.text(said).evaluate().isNotEmpty;
    }
    await connection.stop();
    expect(heard, isTrue, reason: 'a spectator who cannot hear the table is the defect');

    // Both channels are offered here, and only here.
    expect(find.text(t.t('game.chat.tab-spectator')), findsOneWidget);
    expect(find.text(t.t('game.chat.tab-room')), findsOneWidget);

    await tester.tap(find.byIcon(Icons.close));
    await tester.pumpAndSettle(const Duration(seconds: 2));

    // --- tapping the board sends nothing -------------------------------------
    final before = await _get('/api/rooms/$roomId', deps.tokens.access!);
    final movesBefore = ((before['game'] as Map<String, dynamic>)['moves'] as List).length;

    final rect = tester.getRect(find.byType(GameBoard));
    final geometry = BoardGeometry.fit(rows: 15, cols: 15, canvas: Size(rect.width, rect.height));
    await tester.tapAt(rect.topLeft + geometry.centreOf(7, 7));
    await tester.pumpAndSettle(const Duration(seconds: 3));

    final after = await _get('/api/rooms/$roomId', deps.tokens.access!);
    expect(
      ((after['game'] as Map<String, dynamic>)['moves'] as List).length,
      movesBefore,
      reason: 'a spectator must not be able to move',
    );

    // --- leaving takes the spectator route -----------------------------------
    await tester.pageBack();
    await tester.pumpAndSettle(const Duration(seconds: 4));

    final left = await _get('/api/rooms/$roomId', p1['accessToken'] as String);
    expect(
      left['spectators'],
      isEmpty,
      reason: 'DELETE /spectate must have run — POST /leave would not have removed us',
    );
  });
}
