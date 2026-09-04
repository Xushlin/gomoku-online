/// What this person chose to look at. **The only thing that decides the app's theme.**
library;

import 'package:flutter/foundation.dart';

import '../../theme/app_theme.dart';
import '../../i18n/translations.dart';
import '../../theme/board_skin.dart';
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
    required this.skinName,
    required this.locale,
  });

  final String themeName;
  final bool isDark;

  /// Whether sound may play at all. **A third independent axis**, for the same reason
  /// the first two are independent: choosing a theme must not silence the app, and
  /// silencing the app must not change the theme.
  final bool soundOn;

  /// Which board skin. **The fourth axis, and the one that overturned a written rule:**
  /// `add-mobile-settings` recorded that the board's colour follows the theme and that
  /// this client MUST NOT grow a skin axis — with its own dismantling condition. This
  /// is that condition being met.
  final String skinName;

  /// Which language. **The fifth axis**, and the third capability on this client that
  /// existed all along with no way to reach it: both locales have shipped in the bundle
  /// since the first commit, and `AppDependencies` simply hard-coded one.
  final String locale;

  AppSettings copyWith({
    String? themeName,
    bool? isDark,
    bool? soundOn,
    String? skinName,
    String? locale,
  }) => AppSettings(
    themeName: themeName ?? this.themeName,
    isDark: isDark ?? this.isDark,
    soundOn: soundOn ?? this.soundOn,
    skinName: skinName ?? this.skinName,
    locale: locale ?? this.locale,
  );

  @override
  bool operator ==(Object other) =>
      other is AppSettings &&
      other.themeName == themeName &&
      other.isDark == isDark &&
      other.soundOn == soundOn &&
      other.skinName == skinName &&
      other.locale == locale;

  @override
  int get hashCode => Object.hash(themeName, isDark, soundOn, skinName, locale);
}

class SettingsRepository {
  SettingsRepository(this._store, {this.deviceLocale = defaultLocale}) {
    _current = ValueNotifier(_load());
  }

  final PreferencesStore _store;

  /// Injected rather than read from `PlatformDispatcher` here: a unit test has no
  /// device, and "what the phone says" is exactly the branch worth testing.
  final String deviceLocale;
  late final ValueNotifier<AppSettings> _current;

  static const _themeKey = 'gewu.theme';
  static const _darkKey = 'gewu.dark';
  static const _soundKey = 'gewu.sound';
  static const _skinKey = 'gewu.skin';
  static const _localeKey = 'gewu.locale';

  /// **The defaults are exactly what was hard-coded before this existed** (`ink`, dark),
  /// so somebody upgrading sees no change until they choose one.
  static const defaults = AppSettings(
    themeName: defaultThemeName,
    isDark: true,
    soundOn: true,
    skinName: BoardSkin.defaultSkinName,
    locale: defaultLocale,
  );

  /// The language the app used before it could be chosen. Somebody upgrading who has
  /// never picked one, and whose device is not in a supported language, sees no change.
  static const defaultLocale = 'zh-CN';

  /// The device's language, when the app ships it.
  ///
  /// **A fallback, not a source.** Once a person has chosen, their choice stands — a
  /// device-language change MUST NOT overrule it, or "I set it to Chinese" quietly
  /// stops being true after a system update.
  ///
  /// Matched on the exact tag first, then on the language alone: a device reporting
  /// `en-US` should get `en`, and one reporting `zh-Hans-CN` should get `zh-CN`.
  static String localeFromDevice(String deviceTag) {
    if (Translations.supported.containsKey(deviceTag)) return deviceTag;
    final language = deviceTag.split(RegExp('[-_]')).first;
    for (final supported in Translations.supported.keys) {
      if (supported.split('-').first == language) return supported;
    }
    return defaultLocale;
  }

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
      // A skin that is no longer in the artefact falls back rather than painting
      // nothing — skins come from web and one can be removed there.
      skinName: BoardSkin.available.contains(_store.read(_skinKey))
          ? _store.read(_skinKey)!
          : defaults.skinName,
      // Stored choice first; the device's language only when nothing was chosen.
      locale: Translations.supported.containsKey(_store.read(_localeKey))
          ? _store.read(_localeKey)!
          : localeFromDevice(deviceLocale),
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

  Future<void> setLocale(String locale) async {
    if (!Translations.supported.containsKey(locale)) return;
    _current.value = _current.value.copyWith(locale: locale);
    await _store.write(_localeKey, locale);
  }

  Future<void> setSkin(String skinName) async {
    if (!BoardSkin.available.contains(skinName)) return;
    _current.value = _current.value.copyWith(skinName: skinName);
    await _store.write(_skinKey, skinName);
  }

  Future<void> setSoundOn(bool soundOn) async {
    _current.value = _current.value.copyWith(soundOn: soundOn);
    await _store.write(_soundKey, '$soundOn');
  }

  void dispose() => _current.dispose();
}
