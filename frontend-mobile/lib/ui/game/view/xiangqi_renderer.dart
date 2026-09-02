/// 中国象棋's board: ten ranks, nine files, a river between them.
///
/// **Shares [BoardGeometry] with 五子棋 and shares no ornament with it.** 五子棋 has
/// star points and no river; this has a river and two palaces and no star points. That
/// is why the split is one renderer per game rather than one painter with an
/// `if (gameKey == …)` inside it — the latter shape makes the second game edit the
/// first game's code.
library;

import 'dart:math' as math;
import 'dart:ui';

import '../../../data/models/models.dart';
import '../xiangqi/position.dart';
import 'board_geometry.dart';
import 'board_renderer.dart';

/// The glyphs. **Not translated**, and not an oversight: 車 on a 象棋 board is 車 in
/// every locale, the way a chess knight is a horse's head everywhere. The *spoken* name
/// of a piece is a different thing and does go through i18n.
const _glyphs = <int, Map<XiangqiPieceType, String>>{
  redSeat: {
    XiangqiPieceType.general: '帥',
    XiangqiPieceType.advisor: '仕',
    XiangqiPieceType.elephant: '相',
    XiangqiPieceType.horse: '傌',
    XiangqiPieceType.chariot: '俥',
    XiangqiPieceType.cannon: '炮',
    XiangqiPieceType.soldier: '兵',
  },
  blackSeat: {
    XiangqiPieceType.general: '將',
    XiangqiPieceType.advisor: '士',
    XiangqiPieceType.elephant: '象',
    XiangqiPieceType.horse: '馬',
    XiangqiPieceType.chariot: '車',
    XiangqiPieceType.cannon: '砲',
    XiangqiPieceType.soldier: '卒',
  },
};

/// The glyph for [piece].
///
/// Public so a walk can derive **every** glyph from a real board instead of iterating a
/// hand-typed copy of the table above — a list beside the mapper it claims to mirror is
/// the defect this repo has fixed eight times.
String glyphFor(XiangqiPiece piece) => _glyphs[piece.seat]![piece.type]!;

const _red = Color(0xFFC62828);
const _black = Color(0xFF212121);
const _disc = Color(0xFFF3E2C0);

class XiangqiRenderer extends BoardRenderer {
  const XiangqiRenderer();

  /// A piece's disc radius, as a fraction of the step.
  static const discFraction = 0.44;

  /// A glyph's font size, as a fraction of the step.
  ///
  /// **Sized so the ink fits the disc, not so the line box does.** For CJK the line box
  /// is about 1.45× the font size tall, and most of that extra is ascender space no
  /// glyph fills — judging by the box produces a false failure, and judging by width
  /// alone says "it fits" when the diagonal does not. The ink of a CJK glyph is roughly
  /// a square of side = font size, so what has to fit inside the disc is that square's
  /// half-diagonal: `fontSize * √2 / 2 < step * discFraction`. At 0.52 that is
  /// `0.368 * step` against `0.44 * step`. `xiangqi_board_test.dart` measures it with a
  /// real `TextPainter` rather than trusting this arithmetic.
  static const glyphFraction = 0.52;

  @override
  bool get relocates => true;

  @override
  int? seatAt(List<Move> moves, int row, int col) =>
      pieceAt(positionAfter(moves), row, col)?.seat;

  @override
  void paintDecoration(Canvas canvas, BoardGeometry g, Color lineColor) {
    final line = Paint()
      ..color = lineColor
      ..strokeWidth = 1;

    // Ranks run the full width.
    for (var row = 0; row < g.rows; row++) {
      canvas.drawLine(
        Offset(g.xOf(0), g.yOf(row)),
        Offset(g.xOf(g.cols - 1), g.yOf(row)),
        line,
      );
    }

    // Files: the two outer ones are continuous, the inner seven are broken by the
    // river — that gap **is** the river, so it is drawn by not drawing.
    for (var col = 0; col < g.cols; col++) {
      final edge = col == 0 || col == g.cols - 1;
      if (edge) {
        canvas.drawLine(
          Offset(g.xOf(col), g.yOf(0)),
          Offset(g.xOf(col), g.yOf(g.rows - 1)),
          line,
        );
      } else {
        canvas.drawLine(
          Offset(g.xOf(col), g.yOf(0)),
          Offset(g.xOf(col), g.yOf(4)),
          line,
        );
        canvas.drawLine(
          Offset(g.xOf(col), g.yOf(5)),
          Offset(g.xOf(col), g.yOf(g.rows - 1)),
          line,
        );
      }
    }

    // The two palaces, as crossed diagonals over the general's three files.
    for (final top in const [0, 7]) {
      canvas.drawLine(g.centreOf(top, 3), g.centreOf(top + 2, 5), line);
      canvas.drawLine(g.centreOf(top, 5), g.centreOf(top + 2, 3), line);
    }
  }

  @override
  void paintOccupants(
    Canvas canvas,
    BoardGeometry g,
    List<Move> moves,
    (int, int)? selected,
  ) {
    final position = positionAfter(moves);
    final radius = g.step * discFraction;
    final fontSize = g.step * glyphFraction;

    // The origin and destination of the last ply, so "what just happened" is legible.
    final last = moves.isEmpty ? null : moves.last;
    if (last != null && last.isRelocation) {
      final ring = Paint()
        ..color = const Color(0x66000000)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1.5;
      canvas.drawCircle(g.centreOf(last.fromRow!, last.fromCol!), radius * 0.35, ring);
      canvas.drawCircle(g.centreOf(last.row, last.col), radius + 2, ring);
    }

    if (selected != null) {
      canvas.drawCircle(
        g.centreOf(selected.$1, selected.$2),
        radius + 3,
        Paint()
          ..color = const Color(0xFF2E7D32)
          ..style = PaintingStyle.stroke
          ..strokeWidth = 2.5,
      );
    }

    for (var row = 0; row < g.rows; row++) {
      for (var col = 0; col < g.cols; col++) {
        final piece = position[cellIndex(row, col)];
        if (piece == null) continue;
        _drawPiece(canvas, g.centreOf(row, col), radius, fontSize, piece);
      }
    }
  }

  void _drawPiece(
    Canvas canvas,
    Offset centre,
    double radius,
    double fontSize,
    XiangqiPiece piece,
  ) {
    final ink = piece.isRed ? _red : _black;

    canvas.drawCircle(centre, radius, Paint()..color = _disc);
    canvas.drawCircle(
      centre,
      radius,
      Paint()
        ..color = ink
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1.4,
    );

    final builder = ParagraphBuilder(
      ParagraphStyle(textAlign: TextAlign.center, fontSize: fontSize),
    )
      ..pushStyle(TextStyle(color: ink, fontSize: fontSize))
      ..addText(glyphFor(piece));

    final paragraph = builder.build()
      ..layout(ParagraphConstraints(width: radius * 4));

    // Centred on the ink, not on the line box: the box is taller than the glyph and
    // lopsided towards the top, so centring the box drops the glyph visibly low.
    canvas.drawParagraph(
      paragraph,
      Offset(centre.dx - radius * 2, centre.dy - paragraph.height / 2),
    );
  }

  /// The half-diagonal of a glyph's ink at [fontSize], i.e. what has to fit the disc.
  ///
  /// Exposed so a test can compare it against [discFraction] with a real measurement
  /// instead of re-deriving the arithmetic beside the code it is checking.
  static double inkHalfDiagonal(double inkWidth, double inkHeight) =>
      math.sqrt(inkWidth * inkWidth + inkHeight * inkHeight) / 2;
}
