/// The CSS gradients the board skins paint with, reproduced rather than approximated.
///
/// **Two conventions here are easy to get wrong and silent when you do:**
///
/// 1. **In `background-image`, the first layer is on TOP.** Flutter paints in call
///    order, so the layers are drawn back to front — reversed. Get this wrong and the
///    wood's base colour covers its own grain, which looks like "the texture did not
///    come through" rather than like a bug.
/// 2. **CSS angles start at 12 o'clock and go clockwise.** `90deg` is *to the right*,
///    not to the top. A gradient rotated 90° from where it belongs still looks like a
///    gradient.
///
/// Only the two functions the artefact uses are implemented — `radial-gradient` and
/// `repeating-linear-gradient` — and an unrecognised layer returns null rather than
/// being skipped, so a new gradient shape from the web side is visible instead of
/// quietly missing.
library;

import 'dart:math' as math;
import 'dart:ui' as ui;

import 'package:flutter/painting.dart';
import 'package:vector_math/vector_math_64.dart' show Matrix4;

import 'css_value.dart';

/// One `background-image` layer.
abstract class GradientLayer {
  /// The shader to paint over [box], or null when this layer cannot be built.
  ui.Shader shaderFor(Rect box);
}

/// `radial-gradient(<ellipse|circle> at X% Y%, stop…)`, sized `farthest-corner`.
class RadialLayer implements GradientLayer {
  const RadialLayer({
    required this.circle,
    required this.cx,
    required this.cy,
    required this.colors,
    required this.stops,
  });

  /// `circle` keeps one radius; `ellipse` scales the axes independently.
  final bool circle;

  /// Centre, as a fraction of the box.
  final double cx;
  final double cy;

  final List<Color> colors;
  final List<double> stops;

  @override
  ui.Shader shaderFor(Rect box) {
    final centre = Offset(box.left + box.width * cx, box.top + box.height * cy);

    // **`farthest-corner` is the CSS default when no size is given.** For a circle that
    // is the distance to the farthest corner. For an ellipse it is the farthest-side
    // ellipse scaled to pass through that corner — and because the farthest corner sits
    // at exactly (farthest-side-x, farthest-side-y), the scale factor is √2.
    final dx = math.max(centre.dx - box.left, box.right - centre.dx);
    final dy = math.max(centre.dy - box.top, box.bottom - centre.dy);

    if (circle) {
      final r = math.sqrt(dx * dx + dy * dy);
      return ui.Gradient.radial(centre, r, colors, stops);
    }

    final rx = math.sqrt2 * dx;
    final ry = math.sqrt2 * dy;
    // Painted as a circle of radius `ry`, stretched horizontally about the centre.
    final matrix = Matrix4.identity()
      ..translateByDouble(centre.dx, centre.dy, 0, 1)
      ..scaleByDouble(rx / ry, 1, 1, 1)
      ..translateByDouble(-centre.dx, -centre.dy, 0, 1);
    return ui.Gradient.radial(
      centre,
      ry,
      colors,
      stops,
      TileMode.clamp,
      matrix.storage,
    );
  }
}

/// `repeating-linear-gradient(<angle>, stop…)` with pixel stops.
class RepeatingLinearLayer implements GradientLayer {
  const RepeatingLinearLayer({
    required this.angle,
    required this.colors,
    required this.stops,
    required this.periodPx,
  });

  /// Radians, CSS convention: 0 is up, growing clockwise.
  final double angle;
  final List<Color> colors;

  /// Fractions of one period.
  final List<double> stops;
  final double periodPx;

  @override
  ui.Shader shaderFor(Rect box) {
    // CSS 0deg points *up* and grows clockwise; screen y grows downward.
    final dir = Offset(math.sin(angle), -math.cos(angle));
    final from = box.topLeft;
    final to = from + dir * periodPx;
    return ui.Gradient.linear(from, to, colors, stops, TileMode.repeated);
  }
}

/// Parses a `background-image` value into layers, **already in paint order**
/// (back to front — the reverse of how CSS lists them).
///
/// Returns null when the value is `none`, and throws nothing: a caller that gets null
/// paints only the flat background colour, which is what the theme-following skin
/// actually wants.
List<GradientLayer>? parseBackgroundLayers(
  String raw, {
  required Map<String, String> theme,
}) {
  final value = raw.trim();
  if (value.isEmpty || value == 'none') return null;

  final layers = <GradientLayer>[];
  for (final part in splitTopLevel(value)) {
    final layer = _parseLayer(part.trim(), theme);
    // **An unparseable layer aborts the whole stack.** Skipping it would paint a
    // partial texture that looks like a rendering bug in the layers that did work.
    if (layer == null) return null;
    layers.add(layer);
  }
  return layers.reversed.toList();
}

GradientLayer? _parseLayer(String value, Map<String, String> theme) {
  if (value.startsWith('radial-gradient(')) {
    return _radial(value, theme);
  }
  if (value.startsWith('repeating-linear-gradient(')) {
    return _repeatingLinear(value, theme);
  }
  return null;
}

RadialLayer? _radial(String value, Map<String, String> theme) {
  final inner = argsOf(value, 'radial-gradient');
  if (inner == null) return null;
  final parts = splitTopLevel(inner);
  if (parts.length < 2) return null;

  // `ellipse at 50% 50%` / `circle at 32% 26%`
  final head = parts.first.trim().split(RegExp(r'\s+'));
  if (head.length != 4 || head[1] != 'at') return null;
  final circle = head[0] == 'circle';
  if (!circle && head[0] != 'ellipse') return null;

  final cx = parseCssLength(head[2], 1);
  final cy = parseCssLength(head[3], 1);
  if (cx == null || cy == null) return null;

  final colors = <Color>[];
  final stops = <double>[];
  for (final stop in parts.skip(1)) {
    final parsed = _stop(stop, theme, 1);
    if (parsed == null) return null;
    colors.add(parsed.$1);
    stops.add(parsed.$2);
  }
  return RadialLayer(circle: circle, cx: cx, cy: cy, colors: colors, stops: stops);
}

RepeatingLinearLayer? _repeatingLinear(String value, Map<String, String> theme) {
  final inner = argsOf(value, 'repeating-linear-gradient');
  if (inner == null) return null;
  final parts = splitTopLevel(inner);
  if (parts.length < 3) return null;

  final angle = parseCssAngle(parts.first);
  if (angle == null) return null;

  // The period is the last stop's offset, in px.
  final rawStops = parts.skip(1).map((s) => s.trim()).toList();
  final last = rawStops.last.split(RegExp(r'\s+')).last;
  final period = parseCssLength(last, 0);
  if (period == null || period <= 0) return null;

  final colors = <Color>[];
  final stops = <double>[];
  for (final stop in rawStops) {
    final parsed = _stop(stop, theme, period);
    if (parsed == null) return null;
    colors.add(parsed.$1);
    stops.add(parsed.$2);
  }
  return RepeatingLinearLayer(
    angle: angle,
    colors: colors,
    stops: stops,
    periodPx: period,
  );
}

/// `<colour> <offset>` → the colour and its offset as a fraction of [full].
(Color, double)? _stop(String raw, Map<String, String> theme, double full) {
  final value = raw.trim();
  // The offset is the last whitespace-separated token; the colour is everything before
  // it — which can itself contain spaces (`rgb(70 40 12 / 0.18)`), so splitting on
  // whitespace and taking [0] would cut a colour in half.
  final cut = value.lastIndexOf(RegExp(r'\s'));
  if (cut < 0) return null;
  final colour = parseCssColor(value.substring(0, cut), theme: theme);
  final offset = parseCssLength(value.substring(cut + 1), full);
  if (colour == null || offset == null) return null;
  return (colour, full == 0 ? offset : offset / full);
}
