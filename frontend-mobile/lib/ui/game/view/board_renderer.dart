/// How one game's board is drawn.
///
/// **Decoration is per game; geometry is not.** 五子棋 has star points and no river,
/// 中国象棋 has a river and a palace and no star points. So the split is one renderer
/// per game asking a shared [BoardGeometry] — deliberately *not* one painter with an
/// `if (gameKey == …)` inside it, which is the shape that makes the second game edit
/// the first game's code.
library;

import 'dart:ui';

import '../../../data/models/models.dart';
import 'board_geometry.dart';

abstract class BoardRenderer {
  const BoardRenderer();

  /// Lines and ornament. Called before the pieces.
  void paintDecoration(Canvas canvas, BoardGeometry g, Color lineColor);

  /// The occupants, in play order. The last element is the most recent ply.
  void paintOccupants(Canvas canvas, BoardGeometry g, List<Move> moves);
}

/// 五子棋 — a plain grid with star points, stones on the intersections.
class GomokuRenderer extends BoardRenderer {
  const GomokuRenderer();

  /// The lines a real board is marked on, derived from the board's size.
  ///
  /// Standard marking is the **fourth line in from each edge, plus the centre**, which
  /// on a 19-road board is `[3, 9, 15]` and on a 15-road board is `[3, 7, 11]` — and
  /// `[3, 7, 11]` is exactly the literal this used to hard-code. Deriving it is what
  /// makes a non-15 board possible at all: the literal drew its dots **outside** any
  /// smaller board, and a 10×9 board has no star points to draw.
  ///
  /// Empty unless the board is square, odd and at least 9 — those are the boards the
  /// convention is defined for. Guessing on the others would put ornament where no
  /// convention says it belongs.
  static List<int> starLines(int rows, int cols) {
    if (rows != cols || rows < 9 || rows.isEven) return const [];
    return [3, (rows - 1) ~/ 2, rows - 4];
  }

  @override
  void paintDecoration(Canvas canvas, BoardGeometry g, Color lineColor) {
    final line = Paint()
      ..color = lineColor
      ..strokeWidth = 1;

    for (var row = 0; row < g.rows; row++) {
      canvas.drawLine(
        Offset(g.xOf(0), g.yOf(row)),
        Offset(g.xOf(g.cols - 1), g.yOf(row)),
        line,
      );
    }
    for (var col = 0; col < g.cols; col++) {
      canvas.drawLine(
        Offset(g.xOf(col), g.yOf(0)),
        Offset(g.xOf(col), g.yOf(g.rows - 1)),
        line,
      );
    }

    final star = Paint()..color = lineColor;
    final lines = starLines(g.rows, g.cols);
    for (final r in lines) {
      for (final c in lines) {
        canvas.drawCircle(g.centreOf(r, c), 2.5, star);
      }
    }
  }

  @override
  void paintOccupants(Canvas canvas, BoardGeometry g, List<Move> moves) {
    for (var i = 0; i < moves.length; i++) {
      final move = moves[i];
      if (!g.holds(move.row, move.col)) continue;
      final centre = g.centreOf(move.row, move.col);
      final isBlack = move.seat == 0;

      canvas.drawCircle(
        centre,
        g.step * 0.42,
        Paint()..color = isBlack ? const Color(0xFF1A1A1A) : const Color(0xFFF5F5F5),
      );
      canvas.drawCircle(
        centre,
        g.step * 0.42,
        Paint()
          ..color = const Color(0x55000000)
          ..style = PaintingStyle.stroke
          ..strokeWidth = 1,
      );

      // The last stone is marked, because "whose move just happened" is the one thing
      // a static board cannot say.
      if (i == moves.length - 1) {
        canvas.drawCircle(
          centre,
          g.step * 0.14,
          Paint()..color = isBlack ? const Color(0xFFE0E0E0) : const Color(0xFF303030),
        );
      }
    }
  }
}
