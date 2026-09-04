import '../../../data/repositories/auth_repository.dart';
import '../../../data/repositories/settings_repository.dart';
import '../../../i18n/translations.dart';
import '../../../theme/app_theme.dart';
import '../../../theme/board_skin.dart';
import '../../view_model.dart';

/// The settings screen's state and intent.
class SettingsViewModel extends ViewModel {
  SettingsViewModel({required this._settings, required this._auth}) {
    _settings.current.addListener(_onSettingsChanged);
  }

  /// **A named method rather than passing `notifyIfAlive` as a tear-off.** Two reasons,
  /// and the second one is the interesting one: `removeListener` is unambiguous about
  /// what it is removing, and `test/view_model_notify_test.dart` counts files that
  /// *call* `notifyIfAlive(` — a tear-off has no parentheses, so it slipped past the
  /// walk. The walk was right to be narrow; the code was the thing worth changing.
  void _onSettingsChanged() => notifyIfAlive();

  final SettingsRepository _settings;
  final AuthRepository _auth;

  /// **Derived from the synced token artefact, not listed here.** Adding a theme on the
  /// web side makes it appear; a hand-written list would silently not.
  List<String> get themes => AppTheme.availableThemes;

  /// The translation key naming a theme. Every one of [themes] must have copy —
  /// `test/settings_test.dart` walks that, so a theme without a name goes red rather
  /// than rendering its raw key.
  String themeLabelKey(String name) => 'header.theme.$name';

  String get themeName => _settings.current.value.themeName;
  bool get isDark => _settings.current.value.isDark;

  bool get soundOn => _settings.current.value.soundOn;

  /// **Derived from the synced skin artefact**, same as [themes] — a hand-written list
  /// of three names is exactly the shape this repo has fixed nine times.
  List<String> get skins => BoardSkin.available;

  /// **Derived from the shipped locale files**, via `Translations.supported`, which
  /// `test/language_test.dart` pins to the contents of `assets/i18n/`.
  List<String> get locales => Translations.supported.keys.toList()..sort();

  String localeLabelKey(String locale) => 'header.language.$locale';

  String get locale => _settings.current.value.locale;

  String skinLabelKey(String name) => 'header.board-skin.$name';

  String get skinName => _settings.current.value.skinName;

  /// The five axes are independent: setting one MUST NOT reset the others.
  Future<void> chooseTheme(String name) => _settings.setTheme(name);
  Future<void> setDark(bool value) => _settings.setDark(value);
  Future<void> setSoundOn(bool value) => _settings.setSoundOn(value);
  Future<void> chooseSkin(String name) => _settings.setSkin(name);
  Future<void> chooseLocale(String locale) => _settings.setLocale(locale);

  Future<void> signOut() => _auth.logout();

  @override
  void dispose() {
    _settings.current.removeListener(_onSettingsChanged);
    super.dispose();
  }
}
