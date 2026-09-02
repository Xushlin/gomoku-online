/// A board of `rows × cols` intersections, drawn by whichever [BoardRenderer] the
/// game uses.
///
/// **`rows` and `cols`, not one `size`.** The old single number assumed the board was
/// square, which 象棋 (10×9) is not, and it had exactly one caller that always passed
/// 15 — so nothing ever checked that any other value worked. It did not: the star
/// points were the literal `[3, 7, 11]`, which on a smaller board falls outside it.
///
/// Both shapes now have a production caller: 五子棋 at 15×15 and 中国象棋 at 10×9.
library;

import 'package:flutter/material.dart';

import '../../../data/models/models.dart';
import 'board_geometry.dart';
import 'board_renderer.dart';

class GameBoard extends StatelessWidget {
  const GameBoard({
    super.key,
    required this.rows,
    required this.cols,
    required this.renderer,
    required this.moves,
    required this.background,
    required this.onTap,
    this.selected,
  });

  final int rows;
  final int cols;
  final BoardRenderer renderer;

  /// In play order. The renderer decides what an occupant looks like.
  final List<Move> moves;

  final Color background;

  /// The chosen origin, for a game whose renderer [BoardRenderer.relocates].
  final (int, int)? selected;

  /// Called with the intersection tapped. **Not called at all** for a tap on the
  /// letterbox margin a non-square board leaves inside its square box.
  final void Function(int row, int col) onTap;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final side = constraints.biggest.shortestSide;
        final geometry = BoardGeometry.fit(
          rows: rows,
          cols: cols,
          canvas: Size(side, side),
        );

        return SizedBox(
          width: side,
          height: side,
          child: GestureDetector(
            onTapUp: (details) {
              final cell = geometry.cellAt(details.localPosition);
              if (cell != null) onTap(cell.$1, cell.$2);
            },
            child: CustomPaint(
              painter: _BoardPainter(
                geometry: geometry,
                renderer: renderer,
                moves: moves,
                selected: selected,
                background: background,
                lineColor: Theme.of(context).dividerColor,
              ),
            ),
          ),
        );
      },
    );
  }
}

class _BoardPainter extends CustomPainter {
  _BoardPainter({
    required this.geometry,
    required this.renderer,
    required this.moves,
    required this.selected,
    required this.background,
    required this.lineColor,
  });

  final BoardGeometry geometry;
  final BoardRenderer renderer;
  final List<Move> moves;
  final (int, int)? selected;
  final Color background;
  final Color lineColor;

  @override
  void paint(Canvas canvas, Size canvasSize) {
    // The board's own box, not the whole canvas: a 10×9 board must not paint its
    // background across the margin it leaves.
    canvas.drawRect(
      Rect.fromLTWH(
        geometry.originDx,
        geometry.originDy,
        geometry.width,
        geometry.height,
      ),
      Paint()..color = background,
    );
    renderer.paintDecoration(canvas, geometry, lineColor);
    renderer.paintOccupants(canvas, geometry, moves, selected);
  }

  @override
  bool shouldRepaint(_BoardPainter old) =>
      old.moves.length != moves.length ||
      old.selected != selected ||
      old.background != background ||
      old.lineColor != lineColor ||
      old.renderer != renderer ||
      old.geometry.rows != geometry.rows ||
      old.geometry.cols != geometry.cols ||
      old.geometry.step != geometry.step;
}
