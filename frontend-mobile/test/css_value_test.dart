// The CSS subset the skin artefact actually uses.
//
// **The inputs here are copied out of `board-skins.css`, not invented.** A parser tested
// only against values somebody made up passes while failing on the file it exists for.
import 'dart:io';
import 'dart:ui' show Color;

import 'package:flutter_test/flutter_test.dart';

import 'package:gewu_mobile/theme/css_value.dart';

const _theme = {
  'color-surface': '#f8efd8',
  'color-text': '#2b2013',
  'color-border': 'rgb(48 25 8 / 0.85)',
  'color-bg': '#ffffff',
};

void main() {
  group('literals', () {
    test('hex, short hex and rgb with a slash alpha', () {
      expect(parseCssColor('#c88a4e', theme: _theme), const Color(0xFFC88A4E));
      expect(parseCssColor('#abc', theme: _theme), const Color(0xFFAABBCC));
      expect(parseCssColor('rgb(48 25 8 / 0.85)', theme: _theme),
          const Color(0xD9301908));
      expect(parseCssColor('rgb(1, 2, 3)', theme: _theme), const Color(0xFF010203));
    });

    test('transparent is a colour; none is not', () {
      expect(parseCssColor('transparent', theme: _theme), const Color(0x00000000));
      // **`none` must not become black.** It is what the theme-following skin puts in
      // `board-bg-image`, and a colour there is the silent failure this repo already
      // paid for once.
      expect(parseCssColor('none', theme: _theme), isNull);
    });
  });

  group('var()', () {
    test('resolves against the theme, recursively', () {
      expect(parseCssColor('var(--color-surface)', theme: _theme), const Color(0xFFF8EFD8));
      // --color-border is itself an rgb() with alpha.
      expect(parseCssColor('var(--color-border)', theme: _theme), const Color(0xD9301908));
    });

    test('an unknown token with no fallback is null, not a guess', () {
      expect(parseCssColor('var(--nope)', theme: _theme), isNull);
      expect(parseCssColor('var(--nope, #010203)', theme: _theme), const Color(0xFF010203));
    });
  });

  group('color-mix', () {
    test('the exact form the artefact writes', () {
      // Straight from `classic`'s stone fill. **This is why named colours are in the
      // subset at all**: the first version of the parser refused `white` and returned
      // null, which would have made the whole theme-following skin paint nothing.
      final mixed = parseCssColor(
        'color-mix(in srgb, var(--color-text) 65%, white)',
        theme: _theme,
      );
      expect(mixed, isNotNull);
      // 65% of #2b2013 over white.
      expect(mixed!.r * 255, closeTo(0x2b * 0.65 + 255 * 0.35, 1.5));
    });

    test('with both sides parseable it blends in sRGB', () {
      final mixed = parseCssColor(
        'color-mix(in srgb, #000000 25%, #ffffff)',
        theme: _theme,
      );
      expect(mixed, isNotNull);
      expect(mixed!.r * 255, closeTo(191, 1.5)); // 0*0.25 + 255*0.75
    });

    test('nested parens are not cut in half', () {
      // A naive `indexOf(')')` ends the value at `var(--color-text)` and yields a
      // colour that parses — wrong, and wrong in the shape of an answer.
      final mixed = parseCssColor(
        'color-mix(in srgb, var(--color-text) 50%, #ffffff)',
        theme: _theme,
      );
      expect(mixed, isNotNull);
      expect(mixed!.r * 255, closeTo((0x2b + 0xff) / 2, 1.5));
    });
  });

  group('lengths and angles', () {
    test('percentages resolve against the box', () {
      expect(parseCssLength('55%', 200), closeTo(110, 0.001));
      expect(parseCssLength('11px', 200), closeTo(11, 0.001));
      expect(parseCssLength('0', 200), closeTo(0, 0.001));
    });

    test('angles are radians, and a bare number is refused', () {
      expect(parseCssAngle('90deg'), closeTo(1.5707963, 1e-6));
      expect(parseCssAngle('90'), isNull);
    });
  });

  group('the subset is big enough for the artefact', () {
    test('no bare identifier in skin_tokens.g.dart is unhandled', () {
      // **Derived from the artefact, not from the CSS spec.** The accepted named
      // colours are the two the file actually uses; if the web side introduces a third,
      // this goes red rather than the colour silently resolving to null — which is the
      // failure mode that makes a skin look "nearly right" forever.
      final source = File('lib/theme/skin_tokens.g.dart').readAsStringSync();
      const cssKeywords = {
        'in', 'srgb', 'at', 'ellipse', 'circle', 'deg', 'none', 'transparent',
        'inset', 'to', 'top', 'bottom', 'left', 'right', 'closest', 'farthest',
        'side', 'corner', 'gradient', 'radial', 'linear', 'repeating', 'var',
        'color', 'mix', 'rgb', 'rgba', 'const', 'library', 'GENERATED', 'Map',
        'String', 'skinTokens', 'light', 'dark',
      };
      const handledNames = {'white', 'black'};

      final values = RegExp("'[a-z0-9-]+': '(.*?)',\n").allMatches(source);
      expect(values, isNotEmpty, reason: 'a walk over zero values asserts nothing');

      final unknown = <String>{};
      for (final m in values) {
        for (final w in RegExp(r'(?<![-#\w])([a-z]{3,})(?![\w(-])').allMatches(m.group(1)!)) {
          final word = w.group(1)!;
          if (!cssKeywords.contains(word) && !handledNames.contains(word)) {
            unknown.add(word);
          }
        }
      }
      expect(unknown, isEmpty);

      // Non-vacuity: the two we do handle really are in there.
      expect(source.contains('white'), isTrue);
    });
  });
}
