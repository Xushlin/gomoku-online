// Switching language.
//
// **The third time this shape has come up**: the copy for both locales has shipped in
// the bundle since day one and `Translations.load` reads either — `AppDependencies`
// simply hard-coded `'zh-CN'` and loaded once. A capability with no way to reach it
// looks exactly like a missing one.
//
// So the headline assertion here is **the text on screen**, not the stored string:
// `add-mobile-settings` stored a theme perfectly and painted the old one, and
// `add-mobile-board-skins` first asked the token bag instead of the screen.
import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart' show rootBundle;
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'package:gewu_mobile/data/repositories/settings_repository.dart';
import 'package:gewu_mobile/data/repositories/strings_repository.dart';
import 'package:gewu_mobile/data/services/preferences_store.dart';
import 'package:gewu_mobile/i18n/translations.dart';

/// The locales the app actually ships, read from the synced assets.
Set<String> shippedLocales() => Directory('assets/i18n')
    .listSync()
    .whereType<File>()
    .where((f) => f.path.endsWith('.json'))
    .map((f) => f.uri.pathSegments.last.replaceAll('.json', ''))
    .toSet();

Map<String, String> bundle(String locale) {
  Map<String, String> flatten(Map<String, dynamic> json, [String prefix = '']) {
    final out = <String, String>{};
    json.forEach((key, value) {
      final path = prefix.isEmpty ? key : '$prefix.$key';
      if (value is Map<String, dynamic>) {
        out.addAll(flatten(value, path));
      } else {
        out[path] = '$value';
      }
    });
    return out;
  }

  return flatten(
    jsonDecode(File('assets/i18n/$locale.json').readAsStringSync())
        as Map<String, dynamic>,
  );
}

void main() {
  group('the locale list is derived, not typed', () {
    test('supported == the files that actually ship', () {
      // **Equal, not "contains".** A subset assertion goes green when a locale is
      // shipped and never offered — which is the exact failure a hand-written list
      // produces, and this repo has fixed that shape nine times.
      //
      // Positive control: drop one entry from `Translations.supported` and this reds.
      final shipped = shippedLocales();
      expect(shipped, isNotEmpty, reason: 'a walk over zero files asserts nothing');
      expect(shipped.length, greaterThan(1), reason: 'one language is not a choice');
      expect(Translations.supported.keys.toSet(), equals(shipped));
    });

    test('and every one of them has a name, in both locales', () {
      for (final locale in shippedLocales()) {
        for (final inWhich in shippedLocales()) {
          expect(
            bundle(inWhich).containsKey('header.language.$locale'),
            isTrue,
            reason: '$locale has no name in $inWhich',
          );
        }
      }
    });
  });

  group('resolution order', () {
    test('a stored choice wins over the device', () {
      final store = MemoryPreferencesStore()..values['gewu.locale'] = 'zh-CN';
      final settings = SettingsRepository(store, deviceLocale: 'en-US');
      expect(settings.current.value.locale, 'zh-CN',
          reason: 'the device MUST NOT overrule a choice somebody made');
    });

    test('with nothing stored the device decides', () {
      expect(
        SettingsRepository(MemoryPreferencesStore(), deviceLocale: 'en-US')
            .current.value.locale,
        'en',
        reason: 'en-US must match en on the language alone',
      );
    });

    test('and an unsupported device language falls back', () {
      // **Not to a raw key, and not to the device tag.** Either would put text on
      // screen that nobody wrote.
      expect(
        SettingsRepository(MemoryPreferencesStore(), deviceLocale: 'fr-FR')
            .current.value.locale,
        SettingsRepository.defaultLocale,
      );
    });

    test('the matcher itself, both ways', () {
      expect(SettingsRepository.localeFromDevice('en'), 'en');
      expect(SettingsRepository.localeFromDevice('en-GB'), 'en');
      expect(SettingsRepository.localeFromDevice('zh-Hans-CN'), 'zh-CN');
      expect(SettingsRepository.localeFromDevice('de'), SettingsRepository.defaultLocale);
    });

    test('an unsupported choice is refused rather than stored', () async {
      final store = MemoryPreferencesStore();
      final settings = SettingsRepository(store);
      await settings.setLocale('en');
      await settings.setLocale('fr');
      expect(settings.current.value.locale, 'en');
      expect(store.values['gewu.locale'], 'en');
    });

    test('it survives a restart', () async {
      final store = MemoryPreferencesStore();
      await SettingsRepository(store).setLocale('en');
      expect(SettingsRepository(store, deviceLocale: 'zh-CN').current.value.locale, 'en');
    });
  });

  group('the fifth axis disturbs nothing else', () {
    test('and nothing else disturbs it', () async {
      final settings = SettingsRepository(MemoryPreferencesStore());
      await settings.setLocale('en');
      await settings.setTheme('material');
      await settings.setDark(false);
      await settings.setSoundOn(false);
      await settings.setSkin('midnight');
      expect(settings.current.value.locale, 'en');

      await settings.setLocale('zh-CN');
      final now = settings.current.value;
      expect(now.themeName, 'material');
      expect(now.isDark, isFalse);
      expect(now.soundOn, isFalse);
      expect(now.skinName, 'midnight');
    });
  });

  group('the words on screen change', () {
    testWidgets('switching the locale repaints the text, not just the setting',
        (tester) async {
      // **THE assertion of this change.** Twice now a setting has been stored perfectly
      // and painted nowhere — the theme, then the board colour. So this reads the text
      // out of the widget tree, before and after.
      //
      // Positive control: make the switch write the setting without reloading
      // `Translations`, and this goes red while every storage test above stays green.
      TestWidgetsFlutterBinding.ensureInitialized();
      final settings = SettingsRepository(MemoryPreferencesStore());
      final zh = await tester.runAsync(() => Translations.load(rootBundle, 'zh-CN'));
      final strings = StringsRepository(
        bundle: rootBundle,
        settings: settings,
        initial: zh!,
      );

      await tester.pumpWidget(
        ValueListenableBuilder<Translations>(
          valueListenable: strings.current,
          builder: (context, t, _) => Provider<Translations>.value(
            value: t,
            child: MaterialApp(
              home: Builder(
                builder: (context) =>
                    Text(context.watch<Translations>().t('catalog.title')),
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      final chinese = zh.t('catalog.title');
      final english = (await tester.runAsync(
        () => Translations.load(rootBundle, 'en'),
      ))!.t('catalog.title');
      expect(chinese, isNot(english), reason: 'precondition — the two differ');
      expect(find.text(chinese), findsOneWidget, reason: 'precondition — starts Chinese');

      await settings.setLocale('en');
      // The load is asynchronous: give it a real turn of the event loop.
      await tester.runAsync(() => Future<void>.delayed(const Duration(milliseconds: 50)));
      await tester.pumpAndSettle();

      expect(find.text(english), findsOneWidget, reason: 'the SCREEN must change');
      expect(find.text(chinese), findsNothing);

      strings.dispose();
    });
  });
}
