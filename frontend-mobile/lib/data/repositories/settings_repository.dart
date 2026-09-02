/// What this person chose to look at. **The only thing that decides the app's theme.**
library;

import 'package:flutter/foundation.dart';

import '../../theme/app_theme.dart';
import '../services/preferences_store.dart';

/// A theme name and a brightness. Two **orthogonal** axes, the same model the web
/// client uses (`themeName` and `isDark` are separate signals there) — four themes
/// times two modes is not an eight-item list, and flattening it is how "switch to dark"
/// starts silently resetting the theme.
class AppSettings {
  const AppSettings({required this.themeName, required this.isDark});

  final String themeName;
  final bool isDark;

  AppSettings copyWith({String? themeName, bool? isDark}) => AppSettings(
    themeName: themeName ?? this.themeName,
    isDark: isDark ?? this.isDark,
  );

  @override
  bool operator ==(Object other) =>
      other is AppSettings && other.themeName == themeName && other.isDark == isDark;

  @override
  int get hashCode => Object.hash(themeName, isDark);
}

class SettingsRepository {
  SettingsRepository(this._store) {
    _current = ValueNotifier(_load());
  }

  final PreferencesStore _store;
  late final ValueNotifier<AppSettings> _current;

  static const _themeKey = 'gewu.theme';
  static const _darkKey = 'gewu.dark';

  /// **The defaults are exactly what was hard-coded before this existed** (`ink`, dark),
  /// so somebody upgrading sees no change until they choose one.
  static const defaults = AppSettings(themeName: defaultThemeName, isDark: true);

  ValueListenable<AppSettings> get current => _current;

  AppSettings _load() {
    final name = _store.read(_themeKey);
    return AppSettings(
      // A name that is no longer in the synced artefact falls back rather than painting
      // nothing — themes come from web and one can be removed there.
      themeName: name != null && AppTheme.availableThemes.contains(name)
          ? name
          : defaults.themeName,
      isDark: switch (_store.read(_darkKey)) {
        'true' => true,
        'false' => false,
        _ => defaults.isDark,
      },
    );
  }

  Future<void> setTheme(String themeName) async {
    if (!AppTheme.availableThemes.contains(themeName)) return;
    _current.value = _current.value.copyWith(themeName: themeName);
    await _store.write(_themeKey, themeName);
  }

  Future<void> setDark(bool isDark) async {
    _current.value = _current.value.copyWith(isDark: isDark);
    await _store.write(_darkKey, '$isDark');
  }

  void dispose() => _current.dispose();
}
