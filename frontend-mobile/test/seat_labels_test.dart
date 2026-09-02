// What a seat is called, per game.
//
// Two pieces of history are riding on this file. A requirement once put 「象棋读作红 /
// 黑」 in *parentheses* with no mechanism — nothing implemented it, nothing tested it,
// and a Scenario under the same requirement said the opposite; three places followed
// the Scenario and 象棋 rooms called red "black" for a long time. And the criterion was
// written as「座位数大于二」while its stated reason was 「a game with no 白方」 —— 象棋
// and 五子棋 both have exactly two seats, so the case the reason was about went straight
// through the criterion.
import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:gewu_mobile/ui/game/board_registry.dart';
import 'package:gewu_mobile/ui/game/seat_labels.dart';
import 'package:gewu_mobile/ui/game/xiangqi/position.dart';

Map<String, String> flatten(Map<String, dynamic> json, [String prefix = '']) {
  final out = <String, String>{};
  json.forEach((key, value) {
    final path = prefix.isEmpty ? key : '$prefix.$key';
    if (value is Map<String, dynamic>) {
      out.addAll(flatten(value, path));
    } else {
      out[path] = '$value';
    }
  });
  return out;
}

void main() {
  test('象棋 reads seat 0 as red', () {
    expect(seatLabelKey(xiangqiGameKey, 0), 'game.seat.red');
    expect(seatLabelKey(xiangqiGameKey, 1), 'game.seat.black');
  });

  test('五子棋 still reads black then white', () {
    // The other direction. Without it, an implementation that always answers 红 / 黑
    // passes the test above.
    expect(seatLabelKey(gomokuGameKey, 0), 'game.seat.black');
    expect(seatLabelKey(gomokuGameKey, 1), 'game.seat.white');
  });

  test('the two games differ, and they have the same seat count', () {
    // **This is the assertion the old criterion could not make.** Both games have two
    // seats, so any rule dispatching on seat count gives them the same answer — and
    // that is precisely the bug. Dispatching on the game key gives different answers.
    expect(seatLabelKey(xiangqiGameKey, 0), isNot(seatLabelKey(gomokuGameKey, 0)));
  });

  test('a game whose seats have no name falls back to seat numbers', () {
    // 斗地主 has three seats and no colours; answering null is how the caller knows to
    // say "seat N" instead of inventing a colour.
    expect(seatLabelKey('doudizhu', 0), isNull);
    expect(seatLabelKey(xiangqiGameKey, 2), isNull, reason: 'no third seat in 象棋');
    expect(seatLabelKey(xiangqiGameKey, -1), isNull);
  });

  test('every key this file can return has copy in both locales', () {
    // A label that renders as a raw key is the failure mode this whole area keeps
    // producing, and it is invisible to a test that only compares keys to keys.
    for (final locale in const ['zh-CN', 'en']) {
      final bundle = flatten(
        jsonDecode(File('assets/i18n/$locale.json').readAsStringSync())
            as Map<String, dynamic>,
      );
      final keys = [
        for (final game in boardRenderers.keys)
          for (var seat = 0; seat < 2; seat++) seatLabelKey(game, seat),
      ].whereType<String>().toList();

      expect(keys, hasLength(4), reason: 'two games x two seats');
      for (final key in keys) {
        expect(bundle.containsKey(key), isTrue, reason: '$locale $key');
      }
      expect(bundle.containsKey('game.turn.side-turn'), isTrue, reason: locale);
    }
  });

  test('the seat constants agree with the labels', () {
    // `redSeat` lives in the position module and the labels live here; if they ever
    // disagree the board would paint red while the sidebar says black.
    expect(redSeat, 0);
    expect(blackSeat, 1);
    expect(seatLabelKey(xiangqiGameKey, redSeat), 'game.seat.red');
    expect(seatLabelKey(xiangqiGameKey, blackSeat), 'game.seat.black');
  });
}
