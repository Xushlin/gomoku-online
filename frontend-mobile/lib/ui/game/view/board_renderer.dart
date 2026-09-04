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
import '../../../theme/board_skin.dart';
import 'board_geometry.dart';

abstract class BoardRenderer {
  const BoardRenderer();

  /// Whether this game **moves** pieces rather than placing them.
  ///
  /// It lives on the renderer because the board registry is the only place this client
  /// knows anything at all about a game, and "one tap or two" is the same fact as "does
  /// it have pieces that already exist". A game that relocates gets `from → to` and a
  /// selection step; a game that places gets one tap.
  bool get relocates => false;

  /// The seat owning the occupant of `(row, col)`, or null when it is empty.
  ///
  /// Asked by the selection logic, which must not know how any particular game stores
  /// its board: a placement game answers from the move list, a relocation game replays
  /// it. **Neither answer is a legality judgement** — "who is standing here" is not
  /// "may they go there".
  int? seatAt(List<Move> moves, int row, int col);

  /// Lines and ornament. Called before the pieces.
  ///
  /// **Takes the skin, not a colour.** Every colour a board paints comes from there:
  /// the stones used to be two literals in this file, and a literal is exactly what
  /// makes "I changed the skin" turn into "the background changed and the stones did
  /// not". `test/board_skin_test.dart` walks these files for `Color(0x…)`.
  void paintDecoration(Canvas canvas, BoardGeometry g, BoardSkin skin);

  /// The occupants, in play order. The last element is the most recent ply.
  ///
  /// [selected] is the intersection the player has picked as an origin, for a game
  /// where [relocates] is true. Placement games ignore it.
  void paintOccupants(
    Canvas canvas,
    BoardGeometry g,
    List<Move> moves,
    (int, int)? selected,
    BoardSkin skin,
  );
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
  int? seatAt(List<Move> moves, int row, int col) {
    // A placement game's history *is* its board, so the last stone on a point wins —
    // and nothing in 五子棋 ever vacates one.
    for (final move in moves.reversed) {
      if (move.row == row && move.col == col) return move.seat;
    }
    return null;
  }

  @override
  void paintDecoration(Canvas canvas, BoardGeometry g, BoardSkin skin) {
    final line = Paint()
      ..color = skin.line
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

    final star = Paint()..color = skin.star;
    final lines = starLines(g.rows, g.cols);
    for (final r in lines) {
      for (final c in lines) {
        canvas.drawCircle(g.centreOf(r, c), 2.5, star);
      }
    }
  }

  @override
  void paintOccupants(
    Canvas canvas,
    BoardGeometry g,
    List<Move> moves,
    (int, int)? selected,
    BoardSkin skin,
  ) {
    for (var i = 0; i < moves.length; i++) {
      final move = moves[i];
      if (!g.holds(move.row, move.col)) continue;
      final centre = g.centreOf(move.row, move.col);
      final isBlack = move.seat == 0;

      final radius = g.step * 0.42;
      final box = Rect.fromCircle(center: centre, radius: radius);
      // The stone is a gradient in every shipped skin — `radial-gradient(circle at
      // 32% 26%, …)` — so it is painted with that skin's shader over its own box
      // rather than with a flat fill.
      canvas.drawCircle(centre, radius, skin.stonePaint(box, black: isBlack));
      canvas.drawCircle(
        centre,
        radius,
        Paint()
          // The CSS gives stones depth with `box-shadow`, which is not ported; the
          // board's own line colour is the nearest thing the skin does declare.
          ..color = skin.line
          ..style = PaintingStyle.stroke
          ..strokeWidth = 1,
      );

      // The last stone is marked, because "whose move just happened" is the one thing
      // a static board cannot say.
      if (i == moves.length - 1) {
        canvas.drawCircle(
          centre,
          g.step * 0.14,
          // Marked in the *other* stone's colour, so the dot contrasts by
          // construction instead of by two more literals.
          Paint()..color = isBlack ? skin.whiteStoneColor : skin.blackStoneColor,
        );
      }
    }
  }
}
