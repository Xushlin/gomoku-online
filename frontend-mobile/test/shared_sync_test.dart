// The two "do not retype it" rules, enforced.
//
// These read the **real web sources**, not a fixture. A test over a hand-typed
// copy would keep passing while the copy went stale, which is precisely the
// failure it exists to prevent.
import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

import '../tool/sync_shared.dart' as sync;

/// `flutter test` runs from the package root, so these are stable.
final webI18n = Directory('../frontend-web/public/i18n');
final webTokens = File('../frontend-web/src/styles/tokens.css');
final mobileI18n = Directory('assets/i18n');
final generated = File('lib/theme/tokens.g.dart');

void main() {
  group('i18n comes from the web client, not a second translation set', () {
    test('the same locale files are present', () {
      final web = webI18n.listSync().whereType<File>().map((f) => f.uri.pathSegments.last).toSet();
      final mobile =
          mobileI18n.listSync().whereType<File>().map((f) => f.uri.pathSegments.last).toSet();

      expect(mobile, isNotEmpty, reason: 'nothing synced — run dart run tool/sync_shared.dart');
      expect(mobile, equals(web));
    });

    /// **Equality, not containment.** `containsAll` would also pass when the
    /// mobile copy carries half the keys, and a missing key renders as the raw
    /// key on screen.
    test('every locale has exactly the web key set', () {
      for (final file in webI18n.listSync().whereType<File>()) {
        final name = file.uri.pathSegments.last;
        final webKeys = sync.keysOfJson(file.readAsStringSync());
        final mobileKeys = sync.keysOfJson(File('${mobileI18n.path}/$name').readAsStringSync());

        expect(webKeys.length, greaterThan(400), reason: 'sanity: $name should be a full tree');
        expect(
          mobileKeys,
          equals(webKeys),
          reason: '$name is out of sync — run dart run tool/sync_shared.dart',
        );
      }
    });

    test('the two locales agree with each other, so neither has a hole', () {
      final byLocale = {
        for (final f in webI18n.listSync().whereType<File>())
          f.uri.pathSegments.last: sync.keysOfJson(f.readAsStringSync()),
      };
      final locales = byLocale.keys.toList();

      expect(locales.length, greaterThanOrEqualTo(2));
      for (var i = 1; i < locales.length; i++) {
        expect(byLocale[locales[i]], equals(byLocale[locales[0]]));
      }
    });
  });

  group('theme tokens are generated from tokens.css', () {
    test('the committed file is what the generator produces right now', () {
      final expected = sync.renderTokens(sync.parseTokens(webTokens.readAsStringSync()));

      expect(
        generated.readAsStringSync().replaceAll('\r\n', '\n'),
        equals(expected.replaceAll('\r\n', '\n')),
        reason: 'tokens.g.dart is stale — run dart run tool/sync_shared.dart',
      );
    });

    test('parsing found every theme and both modes', () {
      final tokens = sync.parseTokens(webTokens.readAsStringSync());

      // 空解析会让上面那条断言在两边都空时恒真。
      expect(tokens.length, greaterThanOrEqualTo(4), reason: 'themes');
      for (final theme in tokens.values) {
        expect(theme.keys, containsAll(<String>['light', 'dark']));
        expect(theme['light']!.length, greaterThanOrEqualTo(20));
        // Dark must be COMPLETE, not just the overrides the css block lists —
        // reading only the `.dark` block would silently drop most of the palette.
        expect(theme['dark']!.length, equals(theme['light']!.length));
      }
    });

    test('no var() reference survives into the generated file', () {
      // 一个没解开的 `var(--x)` 在 Flutter 里不是颜色,而是一个会静默变成透明的字符串。
      expect(generated.readAsStringSync(), isNot(contains('var(')));
    });
  });

  group('the sanity checks themselves are not vacuous', () {
    test('the web sources are actually there', () {
      expect(webI18n.existsSync(), isTrue);
      expect(webTokens.existsSync(), isTrue);
      expect(jsonDecode(File('${webI18n.path}/en.json').readAsStringSync()), isA<Map>());
    });
  });
}
