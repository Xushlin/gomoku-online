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
  const AppSettings({
    required this.themeName,
    required this.isDark,
    required this.soundOn,
  });

  final String themeName;
  final bool isDark;

  /// Whether sound may play at all. **A third independent axis**, for the same reason
  /// the first two are independent: choosing a theme must not silence the app, and
  /// silencing the app must not change the theme.
  final bool soundOn;

  AppSettings copyWith({String? themeName, bool? isDark, bool? soundOn}) => AppSettings(
    themeName: themeName ?? this.themeName,
    isDark: isDark ?? this.isDark,
    soundOn: soundOn ?? this.soundOn,
  );

  @override
  bool operator ==(Object other) =>
      other is AppSettings &&
      other.themeName == themeName &&
      other.isDark == isDark &&
      other.soundOn == soundOn;

  @override
  int get hashCode => Object.hash(themeName, isDark, soundOn);
}

class SettingsRepository {
  SettingsRepository(this._store) {
    _current = ValueNotifier(_load());
  }

  final PreferencesStore _store;
  late final ValueNotifier<AppSettings> _current;

  static const _themeKey = 'gewu.theme';
  static const _darkKey = 'gewu.dark';
  static const _soundKey = 'gewu.sound';

  /// **The defaults are exactly what was hard-coded before this existed** (`ink`, dark),
  /// so somebody upgrading sees no change until they choose one.
  static const defaults =
      AppSettings(themeName: defaultThemeName, isDark: true, soundOn: true);

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
      soundOn: switch (_store.read(_soundKey)) {
        'true' => true,
        'false' => false,
        _ => defaults.soundOn,
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

  Future<void> setSoundOn(bool soundOn) async {
    _current.value = _current.value.copyWith(soundOn: soundOn);
    await _store.write(_soundKey, '$soundOn');
  }

  void dispose() => _current.dispose();
}
