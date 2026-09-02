// Every translation key the code names exists in every locale — and no widget prints
// a string that never went through the bundle at all.
//
// **The first walk found four holes in shipped code the day it was written**, and all
// four were invisible because `Translations.t` returns the key itself when it is
// missing — which is the right behaviour (the hole is visible on screen) and is exactly
// why nobody noticed: the lobby's create button had been rendering the literal
// `lobby.create.submit`, and its three error messages keys that did not exist.
//
// **The other two walks exist because a real device showed what this one cannot see.**
// `Text('↻')` shipped on two screens and `'${room.status.wire} · …'` printed the
// server's English `Playing` into a Chinese UI. Neither is a *missing key*.
//
// Every list here is **derived from the source**, never typed.
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

bool _isComment(String line) {
  final trimmed = line.trimLeft();
  return trimmed.startsWith('//') || trimmed.startsWith('*') || trimmed.startsWith('/*');
}

Iterable<(File, int, String)> _codeLines(Directory dir) sync* {
  for (final entity in dir.listSync(recursive: true)) {
    if (entity is! File || !entity.path.endsWith('.dart')) continue;
    for (final (index, line) in entity.readAsLinesSync().indexed) {
      if (_isComment(line)) continue;
      yield (entity, index + 1, line);
    }
  }
}

/// Keys named by a `t.t(…)` call or an `errorKey = …` assignment.
///
/// **Line-scoped rather than anchored to `t.t('`**, and that is not laziness: the first
/// version required the quote to follow `t.t(` immediately, so it missed
/// `t.t(vm.registering ? 'auth.register.title' : 'auth.login.title')` — a real call with
/// two real keys. A regex that only matches the *simplest* call shape reports nothing
/// about the others, and its output is indistinguishable from "they are all fine". The
/// non-vacuity test below pins that exact call so the pattern cannot silently narrow
/// again.
///
/// Interpolated keys are skipped on purpose: the literal is not knowable here, and the
/// catalogue's own test covers the one place that builds them.
Set<String> literalKeysUsedIn(Directory dir) {
  final looksLikeKey = RegExp(r"'([a-z][a-z0-9-]*(?:\.[a-z0-9-]+)+)'");
  final found = <String>{};

  for (final (_, _, line) in _codeLines(dir)) {
    if (!line.contains('t.t(') && !line.contains('errorKey =')) continue;
    for (final m in looksLikeKey.allMatches(line)) {
      found.add(m.group(1)!);
    }
  }
  return found;
}

/// `Text('…')` whose literal has no interpolation and is not empty.
List<String> hardCodedDisplayStrings(Directory dir) {
  final literal = RegExp("Text\\(\\s*'([^'\\\$]+)'\\s*[,)]");
  return [
    for (final (file, line, source) in _codeLines(dir))
      for (final m in literal.allMatches(source))
        '${file.path}:$line  Text(${m.group(1)})',
  ];
}

/// Uses of a model's on-the-wire representation inside the UI.
List<String> wireValuesInUi(Directory dir) => [
  for (final (file, line, source) in _codeLines(dir))
    if (source.contains('.wire')) '${file.path}:$line',
];

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
    // Without this, a moved directory or a broken regex leaves the assertion iterating
    // an empty set and passing — the exact shape of the bug it exists for.
    expect(used.length, greaterThan(12), reason: 'literal t.t / errorKey keys under lib/');
    // The ternary call. The first version of the pattern above missed it, and missing it
    // looks exactly like the file being clean.
    expect(used, containsAll(['auth.login.title', 'auth.register.title']));
  });

  test('the bundles are real, so the lookup below can actually fail', () {
    for (final locale in locales) {
      expect(bundles[locale]!.length, greaterThan(400), reason: locale);
    }
  });

  test('the two source walks read real files', () {
    // Non-vacuity for the pair below: if `lib/ui` moved, both would report nothing and
    // pass. Assert the walk sees lines at all.
    expect(_codeLines(Directory('lib/ui')).length, greaterThan(200));
  });

  test('no widget prints a hard-coded display string', () {
    // The rule the web client has had all along: templates MUST NOT hard-code display
    // strings. Mobile had no mechanism for it until a real device showed two.
    expect(hardCodedDisplayStrings(Directory('lib/ui')), equals(<String>[]));
  });

  test('the UI never prints a model wire value', () {
    // `RoomStatus.wire` is what the *server* calls it. Printing it put the English
    // `Playing` on a Chinese screen — not a missing key, so nothing else here sees it.
    expect(wireValuesInUi(Directory('lib/ui')), equals(<String>[]));
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
