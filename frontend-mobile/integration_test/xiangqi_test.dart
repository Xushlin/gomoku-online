// 中国象棋, end to end, through the real widget tree against a real server.
//
//   flutter test integration_test/xiangqi_test.dart -d windows \
//     --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199
//
// **This is the check on the opening setup, and it compares against the authority.**
// The 32 placements are a copy of the server's `XiangqiBoard.Initial()`; a unit test can
// only compare that copy to invariants, because two copies can be wrong together. Here
// a real opening (炮二平五) is played by tapping the two intersections this client
// *believes* the cannon and the centre file to be, and the **server** decides. If the
// client's board were transposed, that tap would land on the wrong piece and the move
// would be refused.
//
// The illegal move goes first, on purpose: it proves the client sends what it is told
// rather than quietly refusing — which is the only way to know the board is not
// judging legality itself (design D2).
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:integration_test/integration_test.dart';

import 'package:gewu_mobile/app.dart';
import 'package:gewu_mobile/i18n/translations.dart';
import 'package:gewu_mobile/data/services/token_store.dart';
import 'package:gewu_mobile/ui/game/board_registry.dart';
import 'package:gewu_mobile/ui/game/view/board_geometry.dart';
import 'package:gewu_mobile/ui/game/view/game_board.dart';
import 'package:gewu_mobile/ui/game/xiangqi/position.dart';

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

/// Where the widget draws intersection `(row, col)`.
Offset intersection(WidgetTester tester, int row, int col) {
  final rect = tester.getRect(find.byType(GameBoard));
  final g = BoardGeometry.fit(
    rows: xiangqiRows,
    cols: xiangqiCols,
    canvas: Size(rect.width, rect.height),
  );
  return rect.topLeft + g.centreOf(row, col);
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
      '象棋 needs a live backend',
      () {},
      skip: 'set --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199; it is NOT set now',
    );
    return;
  }

  testWidgets('炮二平五 is accepted, and an illegal move is refused by the server',
      (tester) async {
    final stamp = DateTime.now().millisecondsSinceEpoch.toString().substring(7);
    // 20 characters: the registration cap, i.e. the longest real content.
    final me = 'xq$stamp'.padRight(20, 'x').substring(0, 20);

    final tokens = MemoryTokenStore();
    final deps = await AppDependencies.build(rootBundle, baseUrl: server, tokenStore: tokens);

    await tester.pumpWidget(GewuApp(deps: deps));
    await tester.pumpAndSettle();

    // --- register through the real form --------------------------------------
    await tester.tap(find.text(deps.strings.t('auth.login.no-account-cta')));
    await tester.pumpAndSettle();
    final fields = find.byType(TextField);
    await tester.enterText(fields.at(0), '$me@example.com');
    await tester.enterText(fields.at(1), me);
    await tester.enterText(fields.at(2), 'Mobile-pass-1234');
    await tester.pumpAndSettle();
    await tester.tap(find.text(deps.strings.t('auth.register.submit')));
    await tester.pumpAndSettle(const Duration(seconds: 6));

    // --- 象棋 is enabled in the catalogue now --------------------------------
    final card = find.text(deps.strings.t('games.$xiangqiGameKey.title'));
    expect(card, findsOneWidget, reason: '象棋 must be listed');
    await tester.tap(card);
    await tester.pumpAndSettle(const Duration(seconds: 4));
    expect(createRoomButton(deps.strings), findsOneWidget, reason: '象棋 lobby');

    // --- create a room ------------------------------------------------------
    await tester.tap(createRoomButton(deps.strings));
    await tester.pumpAndSettle(const Duration(seconds: 6));
    expect(find.byType(GameBoard), findsOneWidget, reason: 'the room should be open');

    // --- find MY room. No fallback: not finding it is the finding. -----------
    final list = jsonDecode(
      (await http.get(
        Uri.parse('$server/api/rooms?gameKey=$xiangqiGameKey'),
        headers: {'authorization': 'Bearer ${tokens.access}'},
      )).body,
    ) as List<dynamic>;
    final mine = list.cast<Map<String, dynamic>>().where(
      (r) => (r['host'] as Map<String, dynamic>?)?['username'] == me,
    );
    expect(mine, hasLength(1), reason: 'exactly one 象棋 room hosted by $me');
    final roomId = mine.single['id'] as String;
    expect(mine.single['gameKey'], xiangqiGameKey);

    // --- an opponent joins, so the game actually starts ----------------------
    final opponent = await _post('/api/auth/register', {
      'email': 'opp$stamp@example.com',
      'username': 'opp$stamp',
      'password': 'Mobile-pass-1234',
    });
    await _post('/api/rooms/$roomId/join', {}, token: opponent['accessToken'] as String);

    final started = await _get('/api/rooms/$roomId', tokens.access!);
    expect(started['status'], 'Playing', reason: 'two seats filled should start the game');
    await tester.pumpAndSettle(const Duration(seconds: 4));

    // --- the illegal move FIRST: the client must send it ---------------------
    // Red's right cannon at (7, 7) to (5, 5), which is in the river and not on any line
    // the cannon can travel. The destination is **empty**, deliberately: tapping one of
    // my own pieces would only re-select, so nothing would leave the client and the
    // server would never get a chance to refuse.
    await tester.tapAt(intersection(tester, 7, 7));
    await tester.pumpAndSettle(const Duration(seconds: 1));
    await tester.tapAt(intersection(tester, 5, 5));
    await tester.pumpAndSettle(const Duration(seconds: 5));

    final afterRefusal = await _get('/api/rooms/$roomId', tokens.access!);
    final refusedMoves = (afterRefusal['game'] as Map<String, dynamic>)['moves'] as List<dynamic>;
    expect(refusedMoves, isEmpty, reason: 'the server must have refused it');
    expect(
      find.text(deps.strings.t('game.errors.invalid-move')),
      findsOneWidget,
      reason: 'and the refusal must be on screen — the client did not pre-judge it',
    );

    // --- 炮二平五 --------------------------------------------------------------
    // Red's right cannon to the centre file. If this client's opening setup were
    // transposed, this tap would pick up a different piece and the server would refuse.
    await tester.tapAt(intersection(tester, 7, 7));
    await tester.pumpAndSettle(const Duration(seconds: 1));
    await tester.tapAt(intersection(tester, 7, 4));
    await tester.pumpAndSettle(const Duration(seconds: 5));

    final after = await _get('/api/rooms/$roomId', tokens.access!);
    final moves = (after['game'] as Map<String, dynamic>)['moves'] as List<dynamic>;

    expect(moves, hasLength(1), reason: 'exactly the one legal move');
    final move = moves.single as Map<String, dynamic>;
    expect(move['fromRow'], 7);
    expect(move['fromCol'], 7);
    expect(move['row'], 7);
    expect(move['col'], 4);
    expect(move['seat'], redSeat, reason: 'red moves first in 象棋');

    debugPrint('XIANGQI OK: room=${after['name']} 炮二平五 = '
        '(${move['fromRow']},${move['fromCol']}) -> (${move['row']},${move['col']}) '
        'seat=${move['seat']}');
  });
}
