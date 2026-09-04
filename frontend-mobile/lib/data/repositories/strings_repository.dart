/// The translations currently in force.
///
/// **A repository rather than state on the shell.** `GewuApp` has to stay a
/// `StatelessWidget` — `test/shell_state_test.dart` pins that with a tear-off the
/// compiler checks — so "which language is loaded" cannot live there. It also cannot be
/// a plain value in a `Provider`: loading is asynchronous and the instance changes, and
/// a `Provider.value` handed a new instance rebuilds nothing on its own.
///
/// So the shell listens to this, and every `context.read<Translations>()` below it gets
/// the new instance when the subtree rebuilds. That works only because **no view holds
/// `Translations` across a rebuild** — checked before writing this: every read is inside
/// a method, none is captured in `initState`.
library;

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart' show AssetBundle;

import '../../i18n/translations.dart';
import 'settings_repository.dart';

class StringsRepository {
  StringsRepository({
    required this.bundle,
    required this.settings,
    required Translations initial,
  })  : _current = ValueNotifier(initial),
        _loadedLocale = initial.locale {
    settings.current.addListener(_onSettingsChanged);
  }

  final AssetBundle bundle;
  final SettingsRepository settings;
  final ValueNotifier<Translations> _current;

  /// What is loaded right now, so a locale change that is already in force does not
  /// start a second load.
  String _loadedLocale;

  ValueListenable<Translations> get current => _current;

  Translations get value => _current.value;

  void _onSettingsChanged() {
    final wanted = settings.current.value.locale;
    if (wanted == _loadedLocale) return;
    _loadedLocale = wanted;
    // Unawaited: the shell keeps showing the language it has until the new one is in.
    // **Never an empty screen** — a half-second of the previous language is a far
    // better failure than a blank one.
    Translations.load(bundle, wanted).then((loaded) {
      // Guard against an out-of-order arrival: two fast switches must not leave the
      // slower load winning.
      if (_loadedLocale == wanted) _current.value = loaded;
    });
  }

  void dispose() {
    settings.current.removeListener(_onSettingsChanged);
    _current.dispose();
  }
}
