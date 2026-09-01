// Every translation key the code names exists in every locale.
//
// **This walk found four holes in shipped code the day it was written**, and all four
// were invisible because `Translations.t` returns the key itself when it is missing —
// which is the right behaviour (the hole is visible on screen) and is exactly why
// nobody noticed: the lobby's create button had been rendering the literal
// `lobby.create.submit`, and its three error messages the literal
// `lobby.errors.load-failed` / `-create-failed` / `-join-failed`. None of those keys
// existed. The correct ones were already in the bundle under different names.
//
// The key list is **derived from the source**, never typed: a hand-written list of
// keys to check is the defect this repo has fixed eight times, and it fails by quietly
// not covering the call somebody just added.
import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

const locales = ['zh-CN', 'en'];

Map<String, String> _flatten(Map<String, dynamic> json, [String prefix = '']) {
  final out = <String, String>{};
  json.forEach((key, value) {
    final path = prefix.isEmpty ? key : '$prefix.$key';
    if (value is Map<String, dynamic>) {
      out.addAll(_flatten(value, path));
    } else {
      out[path] = '$value';
    }
  });
  return out;
}

/// Keys named by a `t.t(…)` call or an `errorKey = …` assignment.
///
/// **Line-scoped rather than anchored to `t.t('`**, and that is not laziness: the first
/// version required the quote to follow `t.t(` immediately, so it missed
/// `t.t(vm.registering ? 'auth.register.title' : 'auth.login.title')` — a real call
/// with two real keys. A regex that only matches the *simplest* call shape reports
/// nothing about the others, and its output is indistinguishable from "they are all
/// fine". The non-vacuity test below pins that exact call so the pattern cannot
/// silently narrow again.
///
/// Interpolated keys (`'games.\$gameKey.title'`) are skipped on purpose: the literal is
/// not knowable here, and the catalogue's own test covers the one place that builds
/// them by asserting against the real bundle.
Set<String> literalKeysUsedIn(Directory dir) {
  final looksLikeKey = RegExp(r"'([a-z][a-z0-9-]*(?:\.[a-z0-9-]+)+)'");
  final found = <String>{};

  for (final entity in dir.listSync(recursive: true)) {
    if (entity is! File || !entity.path.endsWith('.dart')) continue;
    for (final line in entity.readAsLinesSync()) {
      if (!line.contains('t.t(') && !line.contains('errorKey =')) continue;
      if (line.trimLeft().startsWith('//')) continue;
      for (final m in looksLikeKey.allMatches(line)) {
        found.add(m.group(1)!);
      }
    }
  }
  return found;
}

void main() {
  late Set<String> used;
  late Map<String, Map<String, String>> bundles;

  setUpAll(() {
    used = literalKeysUsedIn(Directory('lib'));
    bundles = {
      for (final locale in locales)
        locale: _flatten(
          jsonDecode(File('assets/i18n/$locale.json').readAsStringSync())
              as Map<String, dynamic>,
        ),
    };
  });

  test('the walk found real calls, so the check below is not vacuous', () {
    // Without this, a moved directory or a broken regex leaves the assertion
    // iterating an empty set and passing — the exact shape of the bug it exists for.
    expect(used.length, greaterThan(12), reason: 'literal t.t / errorKey keys under lib/');
    // The ternary call. The first version of the pattern above missed it, and missing
    // it looks exactly like the file being clean.
    expect(used, containsAll(['auth.login.title', 'auth.register.title']));
  });

  test('the bundles are real, so the lookup below can actually fail', () {
    for (final locale in locales) {
      expect(bundles[locale]!.length, greaterThan(400), reason: locale);
    }
  });

  for (final locale in locales) {
    test('every key the code names exists in $locale', () {
      final missing = [
        for (final key in used.toList()..sort())
          if (!bundles[locale]!.containsKey(key)) key,
      ];
      expect(missing, equals(<String>[]));
    });
  }
}
