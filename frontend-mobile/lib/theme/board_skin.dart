/// One board skin, resolved into the handful of things a painter needs.
///
/// **The registry is the synced artefact's keys, not a list here.** `skin_tokens.g.dart`
/// comes from `board-skins.css` via `tool/sync_shared.dart`; a hand-written name list is
/// the defect this repo has fixed nine times, and three names look stable enough to
/// tempt it a tenth.
///
/// **A skin colour is a function of (skin, mode, theme), not of the skin alone.** The
/// `classic` skin is written entirely in `var(--color-*)` and `color-mix()` — it *is*
/// "follow the theme" — so resolution needs the theme's token bag too.
library;

import 'dart:ui' show Brightness;

import 'package:flutter/painting.dart';

import 'board_gradient.dart';
import 'css_value.dart';
import 'skin_tokens.g.dart';
import 'tokens.g.dart';

/// Everything the board paints, and nothing it does not.
class BoardSkin {
  const BoardSkin({
    required this.name,
    required this.background,
    required this.backgroundLayers,
    required this.line,
    required this.star,
    required this.blackStone,
    required this.whiteStone,
    required this.blackStoneColor,
    required this.whiteStoneColor,
    required this.xiangqiPiece,
    required this.xiangqiRed,
    required this.xiangqiBlack,
    required this.selection,
  });

  final String name;

  /// The flat colour under [backgroundLayers]; also the whole board for a skin whose
  /// `board-bg-image` is `none`.
  final Color background;

  /// Already in paint order — back to front. Null when the skin declares `none`.
  final List<GradientLayer>? backgroundLayers;

  final Color line;
  final Color star;

  /// Stones are gradients in every shipped skin (`radial-gradient(circle at 32% 26%…)`),
  /// so they are painted with a shader rather than a flat fill.
  final GradientLayer? blackStone;
  final GradientLayer? whiteStone;

  /// The representative colour of each stone, for the one thing a shader cannot do:
  /// the last-move marker has to contrast with the stone under it.
  final Color blackStoneColor;
  final Color whiteStoneColor;

  final Color xiangqiPiece;
  final Color xiangqiRed;
  final Color xiangqiBlack;

  /// The "you picked this piece" ring.
  ///
  /// **From the theme, not the skin, and that is the honest place for it**: selection is
  /// a UI affordance rather than part of the board's material, and `board-skins.css`
  /// declares no token for it. Taking it from the theme's accent keeps the renderer free
  /// of literals without inventing a skin value the web side does not have.
  final Color selection;

  /// Every skin the artefact declares, sorted so the settings rows do not reorder
  /// between launches (a map literal's order is a property of how somebody typed it).
  static List<String> get available => skinTokens.keys.toList()..sort();

  static const defaultSkinName = 'wood';

  /// Resolves [skinName] for [brightness] against [themeName]'s tokens.
  ///
  /// **Falls back to the default skin by name only** — never by substituting colours.
  /// A skin that is missing from the artefact is a real problem; painting it in
  /// somebody else's colours would hide it, and "nearly right" is the state a skin bug
  /// lives in forever.
  static BoardSkin resolve({
    required String skinName,
    required String themeName,
    required Brightness brightness,
  }) {
    final mode = brightness == Brightness.dark ? 'dark' : 'light';
    final skin = skinTokens[skinName] ?? skinTokens[defaultSkinName]!;
    final values = skin[mode] ?? skin['light']!;
    final theme = themeTokens[themeName]?[mode] ?? const <String, String>{};

    Color colour(String key, Color fallback) =>
        parseCssColor(values[key] ?? '', theme: theme) ?? fallback;

    GradientLayer? stone(String key) {
      final layers = parseBackgroundLayers(values[key] ?? '', theme: theme);
      return (layers == null || layers.isEmpty) ? null : layers.first;
    }

    /// The colour a stone reads as: the **last** stop of its gradient, which is its
    /// body rather than its highlight.
    Color stoneColour(String key, Color fallback) {
      final layer = stone(key);
      if (layer is RadialLayer && layer.colors.isNotEmpty) return layer.colors.last;
      return colour(key, fallback);
    }

    return BoardSkin(
      name: skinName,
      background: colour('board-bg-color', const Color(0xFFD9B382)),
      backgroundLayers: parseBackgroundLayers(values['board-bg-image'] ?? '', theme: theme),
      line: colour('board-line', const Color(0xFF301908)),
      star: colour('board-star', const Color(0xFF261204)),
      blackStone: stone('stone-black-fill'),
      whiteStone: stone('stone-white-fill'),
      blackStoneColor: stoneColour('stone-black-fill', const Color(0xFF1A1A1A)),
      whiteStoneColor: stoneColour('stone-white-fill', const Color(0xFFF5F5F5)),
      xiangqiPiece: colour('xq-piece-bg', const Color(0xFFF3E3C0)),
      xiangqiRed: colour('xq-red', const Color(0xFFB3261E)),
      xiangqiBlack: colour('xq-black', const Color(0xFF241D16)),
      selection: parseCssColor(theme['color-primary'] ?? '', theme: theme) ??
          colour('board-star', const Color(0xFF2E7D32)),
    );
  }

  /// Paints the board's ground over [box] — flat colour, then the layers on top.
  void paintGround(Canvas canvas, Rect box) {
    canvas.drawRect(box, Paint()..color = background);
    for (final layer in backgroundLayers ?? const <GradientLayer>[]) {
      canvas.drawRect(box, Paint()..shader = layer.shaderFor(box));
    }
  }

  /// The paint for one stone occupying [box].
  Paint stonePaint(Rect box, {required bool black}) {
    final layer = black ? blackStone : whiteStone;
    if (layer == null) {
      return Paint()..color = black ? blackStoneColor : whiteStoneColor;
    }
    return Paint()..shader = layer.shaderFor(box);
  }
}
