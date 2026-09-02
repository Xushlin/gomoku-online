/// Where a cell is, in pixels. **The one place the grid arithmetic lives.**
///
/// Two boards share this and nothing else: 五子棋 has star points and no river,
/// 中国象棋 has a river and a palace and no star points. Geometry is what they have in
/// common — so it is a value type both painters ask, not a switch inside one painter.
///
/// **The previous version took a single `size: int`, and that was wrong twice.** It
/// assumed the board was square (象棋 is 10×9, which one number cannot say) and its
/// star points were written as the literal `[3, 7, 11]`, which is only meaningful on a
/// 15-road board. A parameter with one caller that always passes the same value is not
/// a parameter; it is an unverified promise.
///
/// Stones sit on **intersections**, not inside cells. There are `rows` lines one way
/// and `cols` the other, each inset half a step from the edge — that inset is what
/// makes the outermost line of stones look centred rather than clipped.
///
/// (The old doc comment here said the spacing was `size / (n - 1)`. It was not: the
/// code has always divided by `n`. **The code was right and the comment was wrong**,
/// and a wrong formula sends the next reader the wrong way.)
library;

import 'dart:ui' show Offset, Size;

class BoardGeometry {
  const BoardGeometry({
    required this.rows,
    required this.cols,
    required this.step,
    required this.originDx,
    required this.originDy,
  });

  /// The largest board of `rows × cols` intersections that fits in [canvas],
  /// centred. Centring is what letterboxes a 10×9 board inside a square area
  /// instead of stretching it.
  factory BoardGeometry.fit({
    required int rows,
    required int cols,
    required Size canvas,
  }) {
    assert(rows > 0 && cols > 0);
    final step = (canvas.width / cols) < (canvas.height / rows)
        ? canvas.width / cols
        : canvas.height / rows;
    return BoardGeometry(
      rows: rows,
      cols: cols,
      step: step,
      originDx: (canvas.width - cols * step) / 2,
      originDy: (canvas.height - rows * step) / 2,
    );
  }

  final int rows;
  final int cols;

  /// Distance between adjacent intersections. Equal in both directions — a board
  /// with unequal spacing is a stretched board.
  final double step;

  /// Top-left of the board's own box inside the canvas.
  final double originDx;
  final double originDy;

  double get width => cols * step;
  double get height => rows * step;

  /// Half a step in from the board's edge — where the outermost line sits.
  double get inset => step / 2;

  double xOf(int col) => originDx + inset + col * step;
  double yOf(int row) => originDy + inset + row * step;

  Offset centreOf(int row, int col) => Offset(xOf(col), yOf(row));

  bool holds(int row, int col) => row >= 0 && row < rows && col >= 0 && col < cols;

  /// The intersection nearest [point], or null if the point is outside the board.
  ///
  /// **Null rather than clamped**, because a non-square board leaves real empty
  /// margins inside its widget: clamping there turns a tap on the letterbox into a
  /// move on the edge of the board, which is a move the player did not make. On a
  /// board that exactly fills its box (15×15 in a square) there is no such margin, so
  /// nothing changes for 五子棋.
  (int, int)? cellAt(Offset point) {
    final col = ((point.dx - originDx) / step).floor();
    final row = ((point.dy - originDy) / step).floor();
    return holds(row, col) ? (row, col) : null;
  }
}
