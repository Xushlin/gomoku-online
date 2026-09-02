/// Material 3 themes built from the **web client's** design tokens.
///
/// Values come from `tokens.g.dart`, generated out of `tokens.css`. Hand-picking a
/// palette here would be the hand-typed-list defect this repo has fixed eight times:
/// the two clients would drift and nothing would report it.
library;

import 'package:flutter/material.dart';

import 'tokens.g.dart';

/// The theme the app opens with. The others are generated and ready; a picker is
/// out of scope for the first slice, and shipping a picker with no way to test all
/// four is worse than shipping one that works.
const defaultThemeName = 'ink';

/// `#rrggbb` (or `#rgb`) -> [Color].
///
/// Returns null rather than a guess for anything else — a token that is a radius or
/// a gradient is not a colour, and silently turning it into transparent black is how
/// an invisible control ships.
Color? colorOf(String? token) {
  if (token == null) return null;
  final hex = token.trim();
  if (!hex.startsWith('#')) return null;
  final digits = hex.substring(1);
  final expanded = switch (digits.length) {
    3 => digits.split('').map((c) => '$c$c').join(),
    6 => digits,
    8 => digits,
    _ => null,
  };
  if (expanded == null) return null;
  final value = int.tryParse(expanded, radix: 16);
  if (value == null) return null;
  return Color(expanded.length == 8 ? value : 0xFF000000 | value);
}

class AppTheme {
  /// Every theme the synced token artefact carries.
  ///
  /// **Derived from `themeTokens`, never typed out.** A hand-written list posing as a
  /// registry is the defect this repo has fixed eight times, and four theme names look
  /// stable enough to be exactly where it happens again. The artefact comes from
  /// `tool/sync_shared.dart` and is pinned by `shared_sync_test`, so adding a theme on
  /// the web side makes it appear here — and makes the copy walk go red until it has a
  /// name.
  static List<String> get availableThemes => themeTokens.keys.toList()..sort();

  /// Builds a [ThemeData] for one theme name and mode.
  ///
  /// Falls back to Material's own colour only where a token genuinely has no
  /// equivalent — never by inventing a value.
  static ThemeData build(String name, Brightness brightness) {
    final mode = brightness == Brightness.dark ? 'dark' : 'light';
    final tokens = themeTokens[name]?[mode] ?? const {};

    final bg = colorOf(tokens['color-bg']) ?? const Color(0xFF101010);
    final surface = colorOf(tokens['color-surface']) ?? bg;
    final primary = colorOf(tokens['color-primary']) ?? const Color(0xFF1565C0);
    final onPrimary = colorOf(tokens['color-on-primary']) ?? Colors.white;
    final text = colorOf(tokens['color-text']) ?? (brightness == Brightness.dark ? Colors.white : Colors.black);
    final muted = colorOf(tokens['color-muted']) ?? text.withValues(alpha: 0.7);
    final border = colorOf(tokens['color-border']) ?? muted;
    final danger = colorOf(tokens['color-danger']) ?? const Color(0xFFB3261E);

    final scheme = ColorScheme(
      brightness: brightness,
      primary: primary,
      onPrimary: onPrimary,
      secondary: primary,
      onSecondary: onPrimary,
      error: danger,
      onError: Colors.white,
      surface: surface,
      onSurface: text,
    );

    return ThemeData(
      useMaterial3: true,
      brightness: brightness,
      colorScheme: scheme,
      scaffoldBackgroundColor: bg,
      dividerColor: border,
      textTheme: Typography.material2021(colorScheme: scheme).black.apply(
        bodyColor: text,
        displayColor: text,
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: surface,
        hintStyle: TextStyle(color: muted),
        border: OutlineInputBorder(borderSide: BorderSide(color: border)),
        enabledBorder: OutlineInputBorder(borderSide: BorderSide(color: border)),
      ),
      appBarTheme: AppBarTheme(backgroundColor: surface, foregroundColor: text),
      cardTheme: CardThemeData(color: surface),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(backgroundColor: primary, foregroundColor: onPrimary),
      ),
      snackBarTheme: SnackBarThemeData(backgroundColor: surface, contentTextStyle: TextStyle(color: text)),
    );
  }

  /// Board colours, which the web client keeps in its board-skin layer. Only the
  /// three the gomoku board needs are read here; the skin registry itself is a
  /// declared non-goal for this slice.
  static Color boardBackground(String name, Brightness brightness) {
    final mode = brightness == Brightness.dark ? 'dark' : 'light';
    final tokens = themeTokens[name]?[mode] ?? const {};
    return colorOf(tokens['color-well']) ?? colorOf(tokens['color-surface']) ?? const Color(0xFFD9B382);
  }
}
