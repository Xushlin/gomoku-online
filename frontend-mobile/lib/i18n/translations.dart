/// Runtime translations, loaded from the **web client's** locale files.
///
/// The JSON is copied in by `tool/sync_shared.dart` and `test/shared_sync_test.dart`
/// fails if it drifts. There is deliberately no second translation set: 547 keys x 2
/// locales, and two copies diverge with nothing to report it.
library;

import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart' show AssetBundle;

class Translations {
  Translations._(this.locale, this._flat);

  final String locale;
  final Map<String, String> _flat;

  static const supported = <String, String>{'zh-CN': '简体中文', 'en': 'English'};

  static Future<Translations> load(AssetBundle bundle, String locale) async {
    final raw = await bundle.loadString('assets/i18n/$locale.json');
    final tree = jsonDecode(raw) as Map<String, dynamic>;
    return Translations._(locale, flatten(tree));
  }

  /// `{"a": {"b": "x"}}` -> `{"a.b": "x"}` — the same dotted keys the web client uses,
  /// so a key copied out of an Angular template works here unchanged.
  @visibleForTesting
  static Map<String, String> flatten(Map<String, dynamic> tree, [String prefix = '']) {
    final out = <String, String>{};
    for (final entry in tree.entries) {
      final path = prefix.isEmpty ? entry.key : '$prefix.${entry.key}';
      final value = entry.value;
      if (value is Map<String, dynamic>) {
        out.addAll(flatten(value, path));
      } else {
        out[path] = '$value';
      }
    }
    return out;
  }

  int get keyCount => _flat.length;

  /// Looks up a dotted key, substituting `{{name}}` placeholders.
  ///
  /// A missing key returns the key itself — the same visible failure the web client
  /// has, on purpose: silently returning an empty string hides it.
  String t(String key, [Map<String, Object?> params = const {}]) {
    var value = _flat[key];
    if (value == null) return key;
    params.forEach((name, replacement) {
      value = value!.replaceAll('{{$name}}', '$replacement');
    });
    return value!;
  }
}
