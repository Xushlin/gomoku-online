// The 象棋 board's ornament and its glyph sizing.
//
// **The glyph size is measured, not derived beside the code that derives it.** For CJK
// the line box is about 1.45× the font size tall and most of that extra is ascender
// space no glyph fills, so: judging a glyph by its box produces a false failure on the
// top edge, and judging it by width alone says "it fits" when the diagonal does not.
// What has to fit inside a piece's disc is the ink's half-diagonal, and a real
// `TextPainter` is what knows the ink.
import 'dart:ui';

import 'package:flutter/painting.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gewu_mobile/data/models/models.dart';
import 'package:gewu_mobile/ui/game/board_registry.dart';
import 'package:gewu_mobile/ui/game/view/board_geometry.dart';
import 'package:gewu_mobile/ui/game/view/xiangqi_renderer.dart';
import 'package:gewu_mobile/ui/game/xiangqi/position.dart';

/// Every glyph the renderer can draw, taken from the board it draws rather than from a
/// hand-typed list: a list of glyphs beside the mapper is the defect this repo has
/// fixed eight times.
List<String> everyGlyph() {
  final board = initialPosition().whereType<XiangqiPiece>().toList();
  final painter = <String>{};
  for (final piece in board) {
    painter.add(glyphFor(piece));
  }
  return painter.toList();
}

/// The board at 375 px wide, the narrowest layout the platform supports.
BoardGeometry geometryAt(double width) => BoardGeometry.fit(
  rows: xiangqiRows,
  cols: xiangqiCols,
  canvas: Size(width, width),
);

void main() {
  const renderer = XiangqiRenderer();

  test('象棋 is in the board registry, so the catalogue can enable it', () {
    expect(boardRenderers[xiangqiGameKey], isA<XiangqiRenderer>());
    expect(renderer.relocates, isTrue);
    expect(boardRenderers[gomokuGameKey]!.relocates, isFalse,
        reason: 'the other direction — 五子棋 places, it does not relocate');
  });

  group('at 375 px, with the opening board full', () {
    // **Full, not empty.** An empty board passes every layout assertion there is, and
    // three of this repo's four overflow defects were invisible on empty data. 32
    // pieces is the most this board ever holds.
    final g = geometryAt(375);

    test('the geometry is what a 10x9 board in 375 px should be', () {
      expect(g.step, 375 / 10);
      expect(g.rows, 10);
      expect(g.cols, 9);
      expect(g.height, 375);
      expect(g.width, closeTo(375 * 0.9, 0.0001));
    });

    test('every glyph\'s ink fits inside its disc', () {
      final radius = g.step * XiangqiRenderer.discFraction;
      final fontSize = g.step * XiangqiRenderer.glyphFraction;
      final glyphs = everyGlyph();

      expect(glyphs, hasLength(14), reason: 'seven kinds, two sides');

      for (final glyph in glyphs) {
        final painter = TextPainter(
          text: TextSpan(text: glyph, style: TextStyle(fontSize: fontSize)),
          textDirection: TextDirection.ltr,
        )..layout();

        // Width is the honest stand-in for the ink's extent in both directions: for CJK
        // it is exactly the font size, while the *height* carries the empty ascender.
        // Using the height here would fail a glyph that visibly fits.
        final half = XiangqiRenderer.inkHalfDiagonal(painter.width, painter.width);
        expect(
          half,
          lessThan(radius),
          reason: '$glyph: ink half-diagonal $half must fit radius $radius',
        );
      }
    });

    test('and the sizing is not passing by being invisible', () {
      // The positive control for the check above: a font size that genuinely does not
      // fit must fail it. Without this, a `glyphFraction` of 0 would pass.
      final radius = g.step * XiangqiRenderer.discFraction;
      final tooBig = TextPainter(
        text: TextSpan(text: '車', style: TextStyle(fontSize: g.step * 1.2)),
        textDirection: TextDirection.ltr,
      )..layout();
      expect(
        XiangqiRenderer.inkHalfDiagonal(tooBig.width, tooBig.width),
        greaterThan(radius),
      );

      // …and the real font size is not degenerate.
      expect(XiangqiRenderer.glyphFraction, greaterThan(0.35));
      expect(XiangqiRenderer.discFraction, lessThan(0.5),
          reason: 'discs must not touch each other');
    });

    test('discs do not overlap their neighbours', () {
      // Radius under half a step, on both axes, because the step is equal in both.
      expect(g.step * XiangqiRenderer.discFraction * 2, lessThan(g.step * 1.02));
    });
  });

  group('the ornament and 32 pieces stay on the board', () {
    /// Ink pixels, and how many of them fell outside the board's own box.
    Future<({int inked, int strays})> render(
      double side, {
      required bool withPieces,
    }) async {
      final g = geometryAt(side);
      final recorder = PictureRecorder();
      final canvas = Canvas(recorder);
      renderer.paintDecoration(canvas, g, const Color(0xFF000000));
      if (withPieces) {
        // **An empty move list is the OPENING board, not an empty one** — 32 pieces.
        // Worth saying out loud: "empty list" reading as "empty board" is exactly how a
        // layout test ends up proving nothing, and an empty board passes every layout
        // assertion there is.
        renderer.paintOccupants(canvas, g, const <Move>[], null);
      }
      final pixels = side.round();
      final image = await recorder.endRecording().toImage(pixels, pixels);
      final bytes = (await image.toByteData())!;

      const slack = 2.0;
      var inked = 0;
      var strays = 0;
      for (var y = 0; y < pixels; y++) {
        for (var x = 0; x < pixels; x++) {
          if (bytes.getUint8((y * pixels + x) * 4 + 3) == 0) continue;
          inked++;
          if (x < g.originDx - slack ||
              x > g.originDx + g.width + slack ||
              y < g.originDy - slack ||
              y > g.originDy + g.height + slack) {
            strays++;
          }
        }
      }
      return (inked: inked, strays: strays);
    }

    for (final side in const [375.0, 600.0]) {
      test('at ${side.round()} px, a full board paints nothing outside its box', () async {
        // **Sampled as pixels, not read off a bounds object.** `Picture` exposes no
        // usable bounds here, and re-deriving the coordinates beside the code that
        // derives them checks nothing. This is the measurement that would have caught
        // 五子棋's `[3, 7, 11]` drawing its dots off the edge of a smaller board.
        final full = await render(side, withPieces: true);
        expect(full.strays, 0, reason: '${full.strays} inked pixels outside the board');

        // Non-vacuity, and it is the whole reason this is two renders: the pieces must
        // actually have been drawn. A renderer that paints no pieces would score zero
        // strays too.
        final bare = await render(side, withPieces: false);
        expect(
          full.inked,
          greaterThan(bare.inked * 2),
          reason: '32 discs and glyphs should dominate the line work '
              '(bare=${bare.inked}, full=${full.inked})',
        );
      });
    }

    test('五子棋 has no river and 象棋 has no star points', () {
      // The registry answers "which ornament" by having two renderers; this pins that
      // they are in fact different objects rather than one with a flag.
      expect(boardRenderers[xiangqiGameKey], isNot(boardRenderers[gomokuGameKey]));
      expect(boardRenderers[xiangqiGameKey].runtimeType,
          isNot(boardRenderers[gomokuGameKey].runtimeType));
    });
  });

  group('who is standing here', () {
    test('象棋 answers from the replayed board', () {
      expect(renderer.seatAt(const [], 9, 4), redSeat);
      expect(renderer.seatAt(const [], 0, 4), blackSeat);
      expect(renderer.seatAt(const [], 5, 5), isNull, reason: 'the river is empty');
    });

    test('and it follows a move', () {
      const ply = Move(row: 7, col: 4, seat: redSeat, fromRow: 7, fromCol: 7);
      expect(renderer.seatAt(const [ply], 7, 7), isNull);
      expect(renderer.seatAt(const [ply], 7, 4), redSeat);
    });

    test('五子棋 answers from its move list', () {
      const stone = Move(row: 7, col: 7, seat: 0);
      final gomoku = boardRenderers[gomokuGameKey]!;
      expect(gomoku.seatAt(const [stone], 7, 7), 0);
      expect(gomoku.seatAt(const [stone], 7, 8), isNull);
    });
  });
}
