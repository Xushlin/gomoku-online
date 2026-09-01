// The vertical slice, end to end, through the real widget tree.
//
//   flutter test integration_test -d windows \
//     --dart-define=GEWU_PROBE_SERVER=http://localhost:5199
//
// Register -> lobby -> create a room -> a second player joins -> tap the board ->
// the server has that exact move.
//
// **The first version of this test was green and proved almost nothing.** It looked
// for the room it had just created and fell back to `rooms.first` when it could not
// find it — so it asserted against a room left over from unrelated desktop testing
// (`desktop-check`), in a state where the move was refused anyway. Chasing that is
// what surfaced a real bug: the auth response carries the username under `user`, not
// at the top level, so every room was being named `mobile-…`.
//
// There is no fallback now. If the room is not mine, that is the finding.
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart' show rootBundle;
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:integration_test/integration_test.dart';

import 'package:gewu_mobile/app.dart';
import 'package:gewu_mobile/data/services/token_store.dart';
import 'package:gewu_mobile/ui/game/view/gomoku_board.dart';

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

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  if (server.isEmpty) {
    // `testWidgets` takes a bool for `skip`, so the *reason* needs somewhere to
    // live. A skipped test that does not say why is indistinguishable from one
    // nobody wrote.
    test(
      'the slice needs a live backend',
      () {},
      skip: 'set --dart-define=GEWU_PROBE_SERVER=http://localhost:5199 to run this '
          'against a live backend; it is NOT running now',
    );
    return;
  }

  testWidgets('register, open a room, place a stone, and the server records it', (tester) async {
    final stamp = DateTime.now().millisecondsSinceEpoch.toString().substring(7);
    // **20 characters: the registration cap, i.e. the longest real content.**
    // Short names pass every layout assertion — that is the trap this repo keeps
    // re-learning, and three of its four overflow defects were invisible on short
    // or empty data. Flutter reports an overflow as a thrown exception in a debug
    // build, so any clipping here fails the test rather than merely looking odd.
    final me = 'mob$stamp'.padRight(20, 'x').substring(0, 20);

    final tokens = MemoryTokenStore();
    final deps = await AppDependencies.build(
      rootBundle,
      baseUrl: server,
      tokenStore: tokens,
    );

    // The translations must be real, not a stub. A card-table spec in the web client
    // mounts an EMPTY tree, which is why "renders a raw key" is invisible there —
    // this asserts the opposite before touching the UI.
    expect(deps.strings.keyCount, greaterThan(400));
    expect(deps.strings.t('auth.login.title'), isNot('auth.login.title'));

    await tester.pumpWidget(GewuApp(deps: deps));
    await tester.pumpAndSettle();

    // --- register through the actual form -----------------------------------
    await tester.tap(find.text(deps.strings.t('auth.login.no-account-cta')));
    await tester.pumpAndSettle();

    final fields = find.byType(TextField);
    expect(fields, findsNWidgets(3), reason: 'email + username + password');
    await tester.enterText(fields.at(0), '$me@example.com');
    await tester.enterText(fields.at(1), me);
    await tester.enterText(fields.at(2), 'Mobile-pass-1234');
    await tester.pumpAndSettle();

    await tester.tap(find.text(deps.strings.t('auth.register.submit')));
    await tester.pumpAndSettle(const Duration(seconds: 5));

    expect(tokens.access, isNotNull, reason: 'registration should have issued a token');
    expect(await tokens.readRefresh(), isNotNull, reason: 'refresh token must be stored');
    // The username lives under `user`; reading the wrong field made every room
    // `mobile-…`, which looks like a naming choice rather than a bug.
    expect(deps.auth.currentUser?.username, me);

    // --- create a room from the lobby ---------------------------------------
    expect(find.byType(FloatingActionButton), findsOneWidget, reason: 'lobby should be showing');
    await tester.tap(find.byType(FloatingActionButton));
    await tester.pumpAndSettle(const Duration(seconds: 6));

    expect(find.byType(GomokuBoard), findsOneWidget, reason: 'the room should be open');

    // --- find MY room. No fallback: not finding it is the finding. -----------
    final list = jsonDecode(
      (await http.get(
        Uri.parse('$server/api/rooms?gameKey=gomoku'),
        headers: {'authorization': 'Bearer ${tokens.access}'},
      )).body,
    ) as List<dynamic>;

    final mine = list.cast<Map<String, dynamic>>().where(
      (r) => (r['host'] as Map<String, dynamic>?)?['username'] == me,
    );
    expect(mine, hasLength(1), reason: 'exactly one room hosted by $me');
    final roomId = mine.single['id'] as String;

    // --- a second player joins, so the game actually starts ------------------
    // Without this the room stays Waiting and the hub refuses the move — which is
    // the platform working, but it means the tap proves nothing about moves.
    final opponent = await _post('/api/auth/register', {
      'email': 'opp$stamp@example.com',
      'username': 'opp$stamp',
      'password': 'Mobile-pass-1234',
    });
    await _post('/api/rooms/$roomId/join', {}, token: opponent['accessToken'] as String);

    final started = await _get('/api/rooms/$roomId', tokens.access!);
    expect(started['status'], 'Playing', reason: 'two seats filled should start the game');

    // Let the hub push the new state into the widget tree.
    await tester.pumpAndSettle(const Duration(seconds: 4));

    // --- place a stone -------------------------------------------------------
    // Dead centre of a 15x15 board is (7, 7). The server judges legality, not us.
    final board = tester.getRect(find.byType(GomokuBoard));
    await tester.tapAt(board.center);
    await tester.pumpAndSettle(const Duration(seconds: 5));

    // --- the positive control ------------------------------------------------
    // "The tap did not throw" is not evidence. Ask the server what it has, and
    // check the coordinates — a move recorded somewhere else would still be one move.
    final after = await _get('/api/rooms/$roomId', tokens.access!);
    final moves = (after['game'] as Map<String, dynamic>)['moves'] as List<dynamic>;

    expect(moves, hasLength(1), reason: 'the tap should have produced exactly one move');
    final move = moves.single as Map<String, dynamic>;
    expect(move['row'], 7);
    expect(move['col'], 7);

    debugPrint('SLICE OK: room=${after['name']} host=$me status=${after['status']} '
        'move=(${move['row']},${move['col']})');
  });
}
