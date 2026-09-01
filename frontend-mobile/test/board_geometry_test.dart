// The board's geometry, and the ornament that used to be a literal.
//
// The widget took a single `size: int` with one caller that always passed 15, so
// nothing ever checked another value. Two things were wrong behind that: it assumed
// the board was square (象棋 is 10×9), and its star points were the literal
// `[3, 7, 11]`, which on any smaller board falls outside the board entirely.
import 'dart:ui';

import 'package:flutter_test/flutter_test.dart';
import 'package:gewu_mobile/ui/game/view/board_geometry.dart';
import 'package:gewu_mobile/ui/game/view/board_renderer.dart';

void main() {
  group('a 15x15 board is laid out exactly as before', () {
    // The old code: `step = side / size`, `inset = step / 2`, lines at
    // `inset + i * step`. This pins that the extraction changed no pixel.
    const side = 600.0;
    final g = BoardGeometry.fit(rows: 15, cols: 15, canvas: const Size(side, side));

    test('step and inset match the old formula', () {
      expect(g.step, side / 15);
      expect(g.inset, side / 15 / 2);
      expect(g.originDx, 0);
      expect(g.originDy, 0);
    });

    test('the first and last intersections are where they were', () {
      expect(g.centreOf(0, 0), Offset(side / 30, side / 30));
      expect(g.centreOf(14, 14), Offset(side - side / 30, side - side / 30));
    });

    test('the board fills its box, so there is no margin to tap', () {
      expect(g.width, side);
      expect(g.height, side);
      expect(g.cellAt(const Offset(1, 1)), (0, 0));
      expect(g.cellAt(Offset(side - 1, side - 1)), (14, 14));
    });
  });

  group('a 10x9 board keeps its proportions', () {
    const side = 600.0;
    final g = BoardGeometry.fit(rows: 10, cols: 9, canvas: const Size(side, side));

    test('the step is equal in both directions, so nothing is stretched', () {
      // The limiting dimension is the 10 rows, not the 9 columns.
      expect(g.step, side / 10);
      expect(g.height, side);
      expect(g.width, closeTo(side * 9 / 10, 0.0001));
    });

    test('10:9 survives, and the board is centred in its square box', () {
      expect(g.width / g.height, closeTo(9 / 10, 0.0001));
      expect(g.originDx, closeTo((side - g.width) / 2, 0.0001));
      expect(g.originDy, 0);
    });

    test('a tap on the letterbox margin is not a move', () {
      // **Null, not clamped.** Clamping here turns a tap beside the board into a move
      // on its edge — a move the player did not make. A 15×15 board fills its box, so
      // this case does not exist there, which is why it was never noticed.
      expect(g.cellAt(const Offset(1, 300)), isNull);
      expect(g.cellAt(Offset(side - 1, 300)), isNull);
      // ...while a tap inside the board still lands.
      expect(g.cellAt(Offset(g.originDx + 1, 1)), (0, 0));
    });

    test('every intersection is inside the board', () {
      for (var row = 0; row < g.rows; row++) {
        for (var col = 0; col < g.cols; col++) {
          final p = g.centreOf(row, col);
          expect(p.dx, greaterThanOrEqualTo(g.originDx));
          expect(p.dx, lessThanOrEqualTo(g.originDx + g.width));
          expect(p.dy, greaterThanOrEqualTo(g.originDy));
          expect(p.dy, lessThanOrEqualTo(g.originDy + g.height));
        }
      }
    });
  });

  group('star points are derived, not written down', () {
    test('15 roads gives the literal that used to be hard-coded', () {
      expect(GomokuRenderer.starLines(15, 15), [3, 7, 11]);
    });

    test('19 roads gives the standard marking', () {
      // The independent check: `[3, 9, 15]` is what a real 19×19 board is marked with,
      // so the derivation is verified against two known-correct answers rather than
      // against itself.
      expect(GomokuRenderer.starLines(19, 19), [3, 9, 15]);
    });

    test('a small or non-square board has none, so nothing is drawn outside it', () {
      expect(GomokuRenderer.starLines(3, 3), isEmpty);
      expect(GomokuRenderer.starLines(10, 9), isEmpty);
      expect(GomokuRenderer.starLines(8, 8), isEmpty);
    });

    test('and every derived line is inside the board it belongs to', () {
      // The other half. Without it, a derivation that returned e.g. [3, 7, 99] for a
      // 15-road board would still pass the two exact checks above for 19 roads.
      for (final n in const [9, 11, 13, 15, 17, 19, 21]) {
        final lines = GomokuRenderer.starLines(n, n);
        expect(lines, isNotEmpty, reason: '$n roads should be marked');
        for (final line in lines) {
          expect(line, inInclusiveRange(0, n - 1), reason: '$n roads, line $line');
        }
      }
    });
  });
}
