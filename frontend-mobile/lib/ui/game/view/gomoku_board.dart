import 'package:flutter/material.dart';

/// A 15x15 gomoku board.
///
/// Stones are placed on **intersections**, not in cells, so the grid is drawn with
/// `size / (n - 1)` spacing and the outer lines sit half a step inside the padding.
/// Getting that wrong puts every stone half a step off, and the board still looks
/// plausible — which is why the geometry lives in one place with a name.
class GomokuBoard extends StatelessWidget {
  const GomokuBoard({
    super.key,
    required this.stones,
    required this.background,
    required this.onTap,
    this.size = 15,
  });

  /// `[row, col, seat]` per stone, in play order. Seat 0 is black (it moves first).
  final List<List<int>> stones;
  final Color background;
  final void Function(int row, int col) onTap;
  final int size;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final side = constraints.biggest.shortestSide;
        return SizedBox(
          width: side,
          height: side,
          child: GestureDetector(
            onTapUp: (details) {
              final step = side / size;
              final col = (details.localPosition.dx / step).floor().clamp(0, size - 1);
              final row = (details.localPosition.dy / step).floor().clamp(0, size - 1);
              onTap(row, col);
            },
            child: CustomPaint(
              painter: _BoardPainter(
                stones: stones,
                background: background,
                size: size,
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
    required this.stones,
    required this.background,
    required this.size,
    required this.lineColor,
  });

  final List<List<int>> stones;
  final Color background;
  final int size;
  final Color lineColor;

  @override
  void paint(Canvas canvas, Size canvasSize) {
    final side = canvasSize.shortestSide;
    final step = side / size;
    // Half a step of inset puts the outer line inside the board rather than on its
    // edge, which is what makes the last row of stones look centred.
    final inset = step / 2;

    canvas.drawRect(Rect.fromLTWH(0, 0, side, side), Paint()..color = background);

    final line = Paint()
      ..color = lineColor
      ..strokeWidth = 1;
    for (var i = 0; i < size; i++) {
      final at = inset + i * step;
      canvas.drawLine(Offset(inset, at), Offset(side - inset, at), line);
      canvas.drawLine(Offset(at, inset), Offset(at, side - inset), line);
    }

    // Star points, the way a real board is marked.
    final star = Paint()..color = lineColor;
    for (final r in const [3, 7, 11]) {
      for (final c in const [3, 7, 11]) {
        canvas.drawCircle(Offset(inset + c * step, inset + r * step), 2.5, star);
      }
    }

    for (var i = 0; i < stones.length; i++) {
      final stone = stones[i];
      final centre = Offset(inset + stone[1] * step, inset + stone[0] * step);
      final isBlack = stone[2] == 0;
      canvas.drawCircle(
        centre,
        step * 0.42,
        Paint()..color = isBlack ? const Color(0xFF1A1A1A) : const Color(0xFFF5F5F5),
      );
      canvas.drawCircle(
        centre,
        step * 0.42,
        Paint()
          ..color = const Color(0x55000000)
          ..style = PaintingStyle.stroke
          ..strokeWidth = 1,
      );
      // The last stone is marked, because "whose move just happened" is the one
      // thing a static board cannot say.
      if (i == stones.length - 1) {
        canvas.drawCircle(
          centre,
          step * 0.14,
          Paint()..color = isBlack ? const Color(0xFFE0E0E0) : const Color(0xFF303030),
        );
      }
    }
  }

  @override
  bool shouldRepaint(_BoardPainter old) =>
      old.stones.length != stones.length ||
      old.background != background ||
      old.lineColor != lineColor;
}
