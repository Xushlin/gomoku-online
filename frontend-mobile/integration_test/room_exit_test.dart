// Leaving a room, judged by what the SERVER has afterwards.
//
//   flutter test integration_test/room_exit_test.dart -d windows \
//     --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199
//
// **These criteria were measured against the running server, and two of my first
// guesses were wrong.** For the record, because the wrong ones read just as plausible:
//
//   * leaving does **not** vacate the seat — in either state. So "the seat is empty"
//     cannot be the criterion, and a plain `/leave` has no server-observable effect.
//   * dissolve is **`DELETE /api/rooms/{id}`**, not `POST /api/rooms/{id}/dissolve`.
//     The unit test beside the client asserted the wrong path and passed, because a
//     fake adapter accepts any URL. Only the live server said 404.
//
// So the criterion is the **branch**, which the server does make observable:
//
//   host of a Waiting room  -> the room is GONE afterwards (it dissolved)
//   anybody else            -> the room still EXISTS afterwards (it did not)
//
// Get the branch backwards and the server answers 409 or 403, the client shows an
// error, and the player is still in the room — which is exactly what these assert.
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:integration_test/integration_test.dart';
import 'package:signalr_netcore/hub_connection_builder.dart';

import 'package:gewu_mobile/app.dart';
import 'package:gewu_mobile/i18n/translations.dart';
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

Future<int> _roomStatus(String roomId, String token) async {
  final res = await http.get(
    Uri.parse('$server/api/rooms/$roomId'),
    headers: {'authorization': 'Bearer $token'},
  );
  return res.statusCode;
}

Future<({MemoryTokenStore tokens, AppDependencies deps, String me})> _signIn(
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

  expect(find.byType(LoginView), findsNothing, reason: 'registration should have signed us in');
  return (tokens: tokens, deps: deps, me: me);
}

Future<void> _enterGomokuLobby(WidgetTester tester, AppDependencies deps) async {
  await tester.tap(find.text(deps.strings.t('games.$gomokuGameKey.title')));
  await tester.pumpAndSettle(const Duration(seconds: 4));
  expect(createRoomButton(deps.strings), findsOneWidget, reason: 'lobby');
}

/// Taps the AppBar's back arrow, which `PopScope` routes into the leave handler — the
/// same handler the system back gesture reaches.
Future<void> _pressLeave(WidgetTester tester) async {
  await tester.tap(find.byIcon(Icons.arrow_back));
  await tester.pumpAndSettle(const Duration(seconds: 6));
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
      'leaving a room needs a live backend',
      () {},
      skip: 'set --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199; it is NOT set now',
    );
    return;
  }

  testWidgets('the host of a waiting room dissolves it by leaving', (tester) async {
    final signedIn = await _signIn(tester, 'xa');
    await _enterGomokuLobby(tester, signedIn.deps);

    await tester.tap(createRoomButton(signedIn.deps.strings));
    await tester.pumpAndSettle(const Duration(seconds: 6));
    expect(find.byType(GameBoard), findsOneWidget, reason: 'the room should be open');

    final list = jsonDecode(
      (await http.get(
        Uri.parse('$server/api/rooms?gameKey=$gomokuGameKey'),
        headers: {'authorization': 'Bearer ${signedIn.tokens.access}'},
      )).body,
    ) as List<dynamic>;
    // **Filtered by host, and there is no fallback.** Picking "the first Waiting room"
    // is the trap this repo already paid for once: a leftover room from unrelated
    // testing gets asserted against instead of the one under test. It failed loudly
    // here rather than passing quietly, which is the only reason it is worth writing
    // down — the shape is the same.
    final mine = list.cast<Map<String, dynamic>>().where(
      (r) => (r['host'] as Map<String, dynamic>?)?['username'] == signedIn.me,
    );
    expect(mine, hasLength(1), reason: 'exactly one room hosted by ${signedIn.me}');
    expect(mine.single['status'], 'Waiting', reason: 'nobody joined it');
    final roomId = mine.single['id'] as String;

    // Waiting, so no confirmation — the dialog must not appear.
    await _pressLeave(tester);
    expect(find.byType(GameBoard), findsNothing, reason: 'we should be out of the room');

    // **The criterion.** Had the client used `/leave` here, the server answers 409 and
    // the room would still be there.
    expect(
      await _roomStatus(roomId, signedIn.tokens.access!),
      404,
      reason: 'the host leaving a Waiting room dissolves it',
    );
  });

  testWidgets('a guest leaving a game in play does NOT dissolve it', (tester) async {
    // The other direction, and it is what makes the branch load-bearing: `DELETE` in
    // somebody else's room is 403, so a client that always dissolved would pass the
    // test above and strand the player here.
    final stamp = DateTime.now().millisecondsSinceEpoch.toString().substring(7);
    final host = await _post('/api/auth/register', {
      'email': 'oh$stamp@example.com',
      'username': 'oh$stamp',
      'password': 'Mobile-pass-1234',
    });
    final room = await _post(
      '/api/rooms',
      {'name': 'host-room-$stamp', 'gameKey': gomokuGameKey},
      token: host['accessToken'] as String,
    );
    final roomId = room['id'] as String;

    final signedIn = await _signIn(tester, 'xg');
    await _enterGomokuLobby(tester, signedIn.deps);

    // Join the host's room from the list. Two seats filled makes it Playing.
    await tester.tap(find.text('host-room-$stamp'));
    await tester.pumpAndSettle(const Duration(seconds: 6));
    expect(find.byType(GameBoard), findsOneWidget, reason: 'we should be in the room');
    expect(
      await _roomStatus(roomId, signedIn.tokens.access!),
      200,
      reason: 'precondition — the room exists and we are in it',
    );

    // Playing, so the confirmation must appear; take it.
    await tester.tap(find.byIcon(Icons.arrow_back));
    await tester.pumpAndSettle();
    expect(
      find.text(signedIn.deps.strings.t('game.leave-confirm.title')),
      findsOneWidget,
      reason: 'leaving a game in play must ask first',
    );
    await tester.tap(find.text(signedIn.deps.strings.t('game.leave-confirm.leave')));
    await tester.pumpAndSettle(const Duration(seconds: 6));

    expect(find.byType(GameBoard), findsNothing, reason: 'we should be out');
    expect(
      await _roomStatus(roomId, signedIn.tokens.access!),
      200,
      reason: 'a guest leaving MUST NOT dissolve somebody else\'s room',
    );
  });

  testWidgets('a move in a room we left does not repaint the room we are in',
      (tester) async {
    // **This test could not have failed before `fix-mobile-hub-inbound`** — no push
    // ever arrived. Fixing inbound is what made "never called LeaveRoom" live.
    final stamp = DateTime.now().millisecondsSinceEpoch.toString().substring(7);
    final opponent = await _post('/api/auth/register', {
      'email': 'ox$stamp@example.com',
      'username': 'ox$stamp',
      'password': 'Mobile-pass-1234',
    });
    final roomA = await _post(
      '/api/rooms',
      {'name': 'room-a-$stamp', 'gameKey': gomokuGameKey},
      token: opponent['accessToken'] as String,
    );
    final roomAId = roomA['id'] as String;

    final signedIn = await _signIn(tester, 'xp');
    await _enterGomokuLobby(tester, signedIn.deps);

    // --- room A: join, and leave again --------------------------------------
    await tester.tap(find.text('room-a-$stamp'));
    await tester.pumpAndSettle(const Duration(seconds: 6));
    expect(find.byType(GameBoard), findsOneWidget);

    await tester.tap(find.byIcon(Icons.arrow_back));
    await tester.pumpAndSettle();
    await tester.tap(find.text(signedIn.deps.strings.t('game.leave-confirm.leave')));
    await tester.pumpAndSettle(const Duration(seconds: 6));
    expect(find.byType(GameBoard), findsNothing, reason: 'out of A');

    // --- room B: a fresh room of our own ------------------------------------
    await tester.tap(createRoomButton(signedIn.deps.strings));
    await tester.pumpAndSettle(const Duration(seconds: 6));
    expect(find.byType(GameBoard), findsOneWidget, reason: 'in B');

    // --- the opponent, who is seat 0 in A, moves ----------------------------
    final connection = HubConnectionBuilder()
        .withUrl('$server/hubs/match?access_token='
            '${Uri.encodeComponent(opponent['accessToken'] as String)}')
        .build();
    await connection.start();
    await connection.invoke('JoinRoom', args: [roomAId]);
    await connection.invoke('MakeMove', args: [roomAId, 3, 3]);
    await tester.pumpAndSettle(const Duration(seconds: 5));
    await connection.stop();

    // The server really did record it — otherwise this test proves nothing.
    final after = jsonDecode(
      (await http.get(
        Uri.parse('$server/api/rooms/$roomAId'),
        headers: {'authorization': 'Bearer ${opponent['accessToken']}'},
      )).body,
    ) as Map<String, dynamic>;
    expect(
      (after['game'] as Map<String, dynamic>)['moves'],
      hasLength(1),
      reason: 'precondition — A really has a move now',
    );

    // **And B is untouched.** Before this change the client stayed in A's broadcast
    // group and republished A's snapshot over B's board.
    expect(find.byType(GameBoard), findsOneWidget, reason: 'still in B');
    expect(
      find.text(signedIn.deps.strings.t('game.room.status-waiting')),
      findsOneWidget,
      reason: 'B is still Waiting; if A had repainted it, this would say 对局中',
    );
  });
}
