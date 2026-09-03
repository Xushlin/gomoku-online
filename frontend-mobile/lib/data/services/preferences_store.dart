/// Small, non-secret, per-device preferences.
///
/// **Deliberately not `flutter_secure_storage`.** That one holds the refresh token;
/// putting a theme name in the keychain would wear the word "secret" out, and it is
/// slower for something read on every launch.
///
/// An interface rather than a concrete class so a test can substitute an in-memory one
/// without a plugin — `SharedPreferences` needs a platform channel, which unit tests
/// do not have.
library;

import 'package:shared_preferences/shared_preferences.dart';

abstract class PreferencesStore {
  String? read(String key);
  Future<void> write(String key, String value);
}

class SharedPreferencesStore implements PreferencesStore {
  SharedPreferencesStore(this._prefs);

  final SharedPreferences _prefs;

  static Future<SharedPreferencesStore> open() async =>
      SharedPreferencesStore(await SharedPreferences.getInstance());

  @override
  String? read(String key) => _prefs.getString(key);

  @override
  Future<void> write(String key, String value) => _prefs.setString(key, value);
}

/// For tests and for the very first launch, before anything has been chosen.
class MemoryPreferencesStore implements PreferencesStore {
  final values = <String, String>{};

  @override
  String? read(String key) => values[key];

  @override
  Future<void> write(String key, String value) async => values[key] = value;
}
