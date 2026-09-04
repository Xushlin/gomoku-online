/// The little bit of CSS this client has to understand.
///
/// **Not a CSS engine — the exact subset the synced skin artefact actually uses**, and
/// that subset is measured rather than guessed: `board-skins.css` writes colours as
/// `#rrggbb`, `rgb(r g b / a)`, `var(--token)` and `color-mix(in srgb, A p%, B)`, and
/// nothing else. Anything outside it returns null **loudly** (the caller decides), which
/// is the opposite of the failure this repo has already paid for once: a `color-mix()`
/// assigned to `background-image` computed to `none` and the pieces simply had no fill,
/// with nothing thrown and nothing logged.
///
/// `var()` resolves against the **theme** token bag, which is why a skin colour is a
/// function of (skin, mode, theme) rather than of the skin alone. The `classic` skin is
/// entirely written in `var()`s — it *is* "follow the theme" — so a resolver that
/// quietly failed on `var()` would make that whole skin invisible.
library;

import 'dart:math' as math;
import 'dart:ui' show Color;

/// Parses one CSS colour value, resolving `var()` against [theme].
///
/// Returns null when the value is not a colour this subset understands — including
/// `none` and gradient functions. **Null is information**: `BoardSkin` reports it
/// rather than substituting a default, because a wrong-but-plausible colour is exactly
/// what makes a skin look "nearly right" and stay broken.
Color? parseCssColor(String raw, {required Map<String, String> theme, int depth = 0}) {
  // Guards a `var(--a)` cycle in a hand-edited artefact. Ten is far past the two levels
  // the real file uses.
  if (depth > 10) return null;
  final value = raw.trim();
  if (value.isEmpty || value == 'none' || value == 'transparent') {
    return value == 'transparent' ? const Color(0x00000000) : null;
  }

  final named = _namedColours[value];
  if (named != null) return named;

  if (value.startsWith('#')) return _hex(value);

  if (value.startsWith('var(')) {
    final inner = argsOf(value, 'var');
    if (inner == null) return null;
    // `var(--x, fallback)` — the fallback is the part after the first comma.
    final comma = splitTopLevel(inner);
    final name = comma.first.trim().replaceFirst('--', '');
    final resolved = theme[name];
    if (resolved != null) {
      return parseCssColor(resolved, theme: theme, depth: depth + 1);
    }
    return comma.length > 1
        ? parseCssColor(comma[1], theme: theme, depth: depth + 1)
        : null;
  }

  if (value.startsWith('rgb')) return _rgb(value);

  if (value.startsWith('color-mix(')) {
    final inner = argsOf(value, 'color-mix');
    if (inner == null) return null;
    final parts = splitTopLevel(inner);
    // `color-mix(in srgb, A p%, B)` — the only form the artefact uses. Anything else
    // (other spaces, two percentages) is refused rather than approximated.
    if (parts.length != 3 || parts.first.trim() != 'in srgb') return null;

    final first = parts[1].trim();
    final pct = RegExp(r'\s(\d+(?:\.\d+)?)%$').firstMatch(first);
    if (pct == null) return null;
    final weight = double.parse(pct.group(1)!) / 100;

    final a = parseCssColor(
      first.substring(0, pct.start),
      theme: theme,
      depth: depth + 1,
    );
    final b = parseCssColor(parts[2], theme: theme, depth: depth + 1);
    if (a == null || b == null) return null;
    return _mix(a, b, weight);
  }

  return null;
}

/// sRGB mix, [weight] being how much of [a] survives. Matches `color-mix(in srgb, …)`
/// for opaque inputs, which is all the artefact mixes.
Color _mix(Color a, Color b, double weight) {
  int c(double x, double y) => (x * weight + y * (1 - weight)).round().clamp(0, 255);
  return Color.fromARGB(
    c(a.a * 255, b.a * 255),
    c(a.r * 255, b.r * 255),
    c(a.g * 255, b.g * 255),
    c(a.b * 255, b.b * 255),
  );
}

/// CSS named colours, **and only the ones the artefact actually writes**.
///
/// Counted out of `skin_tokens.g.dart` rather than copied from the CSS spec: exactly
/// two appear (`white` in the theme-following skin's stone fills, `black` in a shadow).
/// A list of all 148 named colours would be a hand-written registry nobody checks, and
/// the two that matter would be lost in it.
///
/// `test/css_value_test.dart` walks the artefact for bare identifiers and fails on any
/// that is neither a CSS keyword nor a key here — so a third one arriving from the web
/// side goes red instead of silently resolving to null.
const _namedColours = <String, Color>{
  'white': Color(0xFFFFFFFF),
  'black': Color(0xFF000000),
};

Color? _hex(String value) {
  var hex = value.substring(1);
  if (hex.length == 3) {
    hex = hex.split('').map((c) => '$c$c').join();
  }
  if (hex.length == 6) hex = 'ff$hex';
  if (hex.length != 8) return null;
  final n = int.tryParse(hex, radix: 16);
  return n == null ? null : Color(n);
}

/// `rgb(r g b / a)` and `rgb(r, g, b)`; `rgba()` behaves the same here.
Color? _rgb(String value) {
  final inner = argsOf(value, value.startsWith('rgba') ? 'rgba' : 'rgb');
  if (inner == null) return null;

  final slash = inner.split('/');
  final channels = slash.first
      .replaceAll(',', ' ')
      .split(RegExp(r'\s+'))
      .where((s) => s.isNotEmpty)
      .toList();
  if (channels.length < 3) return null;

  final rgb = <int>[];
  for (final c in channels.take(3)) {
    final n = double.tryParse(c);
    if (n == null) return null;
    rgb.add(n.round().clamp(0, 255));
  }

  var alpha = 1.0;
  if (slash.length > 1) {
    final a = double.tryParse(slash[1].trim());
    if (a == null) return null;
    alpha = a.clamp(0.0, 1.0);
  } else if (channels.length > 3) {
    final a = double.tryParse(channels[3]);
    if (a == null) return null;
    alpha = a.clamp(0.0, 1.0);
  }
  return Color.fromARGB((alpha * 255).round(), rgb[0], rgb[1], rgb[2]);
}

/// The text between `name(` and its matching `)`. Null when unbalanced.
///
/// Public because `board_gradient.dart` needs the same two primitives — a second copy
/// of a paren-counting splitter is exactly the kind of duplicate that drifts.
///
/// **Counts nesting** — `color-mix(in srgb, var(--x) 65%, white)` has an inner `(`, and
/// a naive `indexOf(')')` would cut the value in half and produce a colour that parses.
String? argsOf(String value, String name) {
  if (!value.startsWith('$name(')) return null;
  var depth = 0;
  for (var i = name.length; i < value.length; i++) {
    if (value[i] == '(') depth++;
    if (value[i] == ')') {
      depth--;
      if (depth == 0) return value.substring(name.length + 1, i);
    }
  }
  return null;
}

/// Splits on commas that are not inside parentheses.
List<String> splitTopLevel(String value) {
  final out = <String>[];
  var depth = 0;
  var start = 0;
  for (var i = 0; i < value.length; i++) {
    final c = value[i];
    if (c == '(') depth++;
    if (c == ')') depth--;
    if (c == ',' && depth == 0) {
      out.add(value.substring(start, i));
      start = i + 1;
    }
  }
  out.add(value.substring(start));
  return out;
}

/// A CSS length or percentage, resolved against [full] (the box's extent).
double? parseCssLength(String raw, double full) {
  final value = raw.trim();
  if (value.endsWith('%')) {
    final n = double.tryParse(value.substring(0, value.length - 1));
    return n == null ? null : n / 100 * full;
  }
  if (value.endsWith('px')) {
    return double.tryParse(value.substring(0, value.length - 2));
  }
  return double.tryParse(value);
}

/// Degrees as CSS writes them (`90deg`), in radians, with CSS's 0° = up convention.
double? parseCssAngle(String raw) {
  final value = raw.trim();
  if (!value.endsWith('deg')) return null;
  final deg = double.tryParse(value.substring(0, value.length - 3));
  return deg == null ? null : deg * math.pi / 180;
}
