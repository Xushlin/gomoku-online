// The wood grain, checked as pixels rather than as prose.
//
// **The input is the artefact, not a value typed into this file.** `board-skins.css`'s
// `--board-bg-image` for `wood` is a four-layer stack; this parses that exact string and
// then paints it, so "the texture came through" is a measurement of the same text the
// web client renders.
//
// Two conventions are asserted because getting either wrong is silent:
//
//   * CSS lists background layers **top first**; the painter must draw them reversed, or
//     the base colour covers its own grain and it looks like the texture is missing;
//   * `90deg` in CSS is **to the right**, not up.
import 'dart:ui' as ui;

import 'package:flutter/painting.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:gewu_mobile/theme/board_gradient.dart';
import 'package:gewu_mobile/theme/skin_tokens.g.dart';

/// Paints the layers over a [size]×[size] box and reads the pixels back.
Future<ui.Image> _render(List<GradientLayer> layers, Color base, int size) async {
  final recorder = ui.PictureRecorder();
  final canvas = Canvas(recorder);
  final box = Rect.fromLTWH(0, 0, size.toDouble(), size.toDouble());
  canvas.drawRect(box, Paint()..color = base);
  for (final layer in layers) {
    canvas.drawRect(box, Paint()..shader = layer.shaderFor(box));
  }
  return recorder.endRecording().toImage(size, size);
}

Future<Color Function(int, int)> _sampler(ui.Image image) async {
  final data = (await image.toByteData(format: ui.ImageByteFormat.rawRgba))!;
  return (int x, int y) {
    final i = (y * image.width + x) * 4;
    return Color.fromARGB(
      data.getUint8(i + 3),
      data.getUint8(i),
      data.getUint8(i + 1),
      data.getUint8(i + 2),
    );
  };
}

void main() {
  const theme = <String, String>{};
  final woodImage = skinTokens['wood']!['light']!['board-bg-image']!;

  group('the four layers of the wood board', () {
    test('parse, and come back in paint order — reversed', () {
      final layers = parseBackgroundLayers(woodImage, theme: theme);
      expect(layers, isNotNull, reason: 'the artefact value must parse');
      expect(layers!, hasLength(4));

      // CSS order is vignette, fibres, cross-grain, base. Painting order is the
      // reverse, so the **base** must be first out.
      expect(layers.first, isA<RadialLayer>());
      final base = layers.first as RadialLayer;
      expect(base.cx, closeTo(0.28, 1e-9), reason: 'the base highlight sits at 28% 22%');
      expect(base.cy, closeTo(0.22, 1e-9));
      expect(base.colors.first, const Color(0xFFECCA8D));
      expect(base.colors.last, const Color(0xFFB78342));
      expect(base.stops, [closeTo(0, 1e-9), closeTo(0.55, 1e-9), closeTo(1, 1e-9)]);

      // …and the vignette must be last out, i.e. on top.
      expect(layers.last, isA<RadialLayer>());
      expect((layers.last as RadialLayer).cx, closeTo(0.5, 1e-9));
    });

    test('the two repeating layers keep their periods and directions', () {
      final layers = parseBackgroundLayers(woodImage, theme: theme)!;
      final repeating = layers.whereType<RepeatingLinearLayer>().toList();
      expect(repeating, hasLength(2));

      // Painted back-to-front, so the cross-grain (0deg, 5px) comes before the
      // fibres (90deg, 12px).
      expect(repeating[0].periodPx, closeTo(5, 1e-9));
      expect(repeating[0].angle, closeTo(0, 1e-9), reason: '0deg — horizontal banding');
      expect(repeating[1].periodPx, closeTo(12, 1e-9));
      expect(
        repeating[1].angle,
        closeTo(1.5707963, 1e-6),
        reason: '90deg — CSS 90 is to the RIGHT, so the fibres run vertically',
      );
    });
  });

  group('painted', () {
    test('the base highlight really is #ecca8d where CSS puts it', () async {
      // At (28%, 22%) the base radial is at stop 0. The vignette above it is fully
      // transparent inside 55% of its radius, and that point is at ~0.50 — so the only
      // thing that can tint this pixel is the low-alpha grain.
      final layers = parseBackgroundLayers(woodImage, theme: theme)!;
      final image = await _render(layers, const Color(0xFFC88A4E), 200);
      final at = await _sampler(image);

      final c = at(56, 44);
      expect(c.r * 255, closeTo(0xEC, 8), reason: 'red');
      expect(c.g * 255, closeTo(0xCA, 8), reason: 'green');
      expect(c.b * 255, closeTo(0x8D, 8), reason: 'blue');
    });

    test('the vertical fibres are actually on the board, every 12 px', () async {
      // **The assertion that "the texture came through".** A structural test passes
      // when the layers parse and are never drawn; this walks a horizontal run of real
      // pixels and requires the 12 px pattern to be visible in them.
      final layers = parseBackgroundLayers(woodImage, theme: theme)!;
      final image = await _render(layers, const Color(0xFFC88A4E), 200);
      final at = await _sampler(image);

      final row = [for (var x = 0; x < 48; x++) at(x, 100)];
      expect(row.toSet().length, greaterThan(1), reason: 'a flat row means no grain');

      // **Periodicity is asserted on the fibre layer alone, and the first version of
      // this test got that wrong.** Comparing two pixels of the *finished* board 12 px
      // apart assumes everything under them is constant — and the base radial is not:
      // it varies across x, so the two differed and the failure read as "the pattern
      // does not repeat" when the pattern was fine.
      final fibres = layers.whereType<RepeatingLinearLayer>()
          .firstWhere((l) => l.periodPx == 12);
      final only = await _render([fibres], const Color(0xFFC88A4E), 200);
      final fibreAt = await _sampler(only);

      expect(fibreAt(11, 100), fibreAt(23, 100), reason: 'repeats every 12 px');
      expect(fibreAt(11, 100), fibreAt(35, 100), reason: 'and again');
      expect(fibreAt(11, 100), isNot(fibreAt(5, 100)), reason: 'and is not uniform');
    });

    test('a skin with no background image paints only its colour', () async {
      // `classic` writes `none` — and `none` must not become a layer. This is the same
      // family as the web bug where a colour assigned to `background-image` computed to
      // `none` and the pieces had no fill at all.
      final classic = skinTokens['classic']!['light']!['board-bg-image']!;
      expect(classic, 'none', reason: 'precondition — the artefact really says none');
      expect(parseBackgroundLayers(classic, theme: theme), isNull);
    });
  });

  group('midnight is a different board, not a tinted one', () {
    test('its base radial differs from wood at the same point', () async {
      Future<Color> centreOf(String skin) async {
        final value = skinTokens[skin]!['light']!['board-bg-image']!;
        final layers = parseBackgroundLayers(value, theme: theme)!;
        final image = await _render(layers, const Color(0xFF000000), 200);
        return (await _sampler(image))(56, 44);
      }

      expect(await centreOf('midnight'), isNot(await centreOf('wood')));
    });
  });
}
