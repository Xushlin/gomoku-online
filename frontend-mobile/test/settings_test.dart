// Choosing what the app looks like, and asking before signing out.
//
// **Three of these four complaints came off a real phone**, and the third one is the
// one worth reading twice: 「现在无法更换主题」. The themes had been synced from the web
// client since the first commit here and `AppTheme.build` could paint all four — there
// was simply no way to say which one, and the app hard-coded `ink` + dark on every
// launch. **A capability with no way to reach it looks exactly like a missing one.**
//
// So the assertions come in two halves, and the second half is the one that would have
// caught the original defect:
//
//   1. the choice is stored, restored, and the two axes do not disturb each other;
//   2. **the choice changes the painted `ThemeData`** — otherwise a screen that stores
//      the string perfectly and looks identical passes every test in half 1.
import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart' show rootBundle;
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'package:gewu_mobile/data/repositories/auth_repository.dart';
import 'package:gewu_mobile/data/repositories/settings_repository.dart';
import 'package:gewu_mobile/data/services/dio_client.dart';
import 'package:gewu_mobile/data/services/preferences_store.dart';
import 'package:gewu_mobile/data/services/token_store.dart';
import 'package:gewu_mobile/i18n/translations.dart';
import 'package:gewu_mobile/theme/app_theme.dart';
import 'package:gewu_mobile/ui/settings/view/settings_view.dart';
import 'package:gewu_mobile/ui/settings/view_model/settings_view_model.dart';
import 'package:gewu_mobile/ui/view_model.dart';

class _LoginAdapter implements HttpClientAdapter {
  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async => ResponseBody.fromString(
    jsonEncode({
      'accessToken': 'a',
      'refreshToken': 'r',
      'user': {'id': 'u1', 'username': 'me'},
    }),
    200,
    headers: {
      Headers.contentTypeHeader: [Headers.jsonContentType],
    },
  );

  @override
  void close({bool force = false}) {}
}

/// A repository that has actually been through `login`.
///
/// **Setting `currentUser` by hand would not do**: `signedIn` is a private notifier
/// that only `_adopt` flips, and `signedIn` is what the router — and therefore this
/// whole feature — reads. A fixture that fakes the field and not the notifier would
/// make "cancel did not sign out" green while never having been signed in at all.
///
/// **It has to run under `tester.runAsync`, and finding that out cost ten minutes of
/// wall clock.** `testWidgets` runs in a fake-clock zone; Dio arms a real `Timer` for
/// its send timeout, so `await login(...)` there simply never returns — and
/// `pumpAndSettle` advances the *fake* clock, so the 30-second test timeout never
/// fires either. The symptom is a test that burns CPU until the 10-minute
/// `pumpAndSettle` ceiling, which reads like an infinite animation rather than a
/// stalled future.
Future<AuthRepository> signedInAuth(WidgetTester tester, MemoryTokenStore tokens) async {
  final auth = await tester.runAsync(() async {
    final dio = buildDio(
      baseUrl: 'http://example.invalid',
      tokens: tokens,
      refresh: () async => false,
      adapter: _LoginAdapter(),
    );
    final repo = AuthRepository(dio: dio, tokens: tokens);
    await repo.login('me@example.invalid', 'x');
    return repo;
  });
  return auth!;
}

/// Never logged in. Enough for the tests that only watch notifications.
AuthRepository idleAuth() => AuthRepository(
  dio: buildDio(
    baseUrl: 'http://example.invalid',
    tokens: MemoryTokenStore(),
    refresh: () async => false,
    adapter: _LoginAdapter(),
  ),
  tokens: MemoryTokenStore(),
);

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

Map<String, String> bundle(String locale) =>
    flatten(jsonDecode(File('assets/i18n/$locale.json').readAsStringSync())
        as Map<String, dynamic>);

/// A settings screen over an in-memory store, mounted for real.
Future<SettingsViewModel> pumpSettings(
  WidgetTester tester, {
  required SettingsRepository settings,
  required AuthRepository auth,
  required Translations strings,
}) async {
  final vm = SettingsViewModel(settings: settings, auth: auth);
  await tester.pumpWidget(
    MultiProvider(
      providers: [
        Provider<Translations>.value(value: strings),
        ChangeNotifierProvider<SettingsViewModel>.value(value: vm),
      ],
      child: const MaterialApp(home: SettingsView()),
    ),
  );
  await tester.pumpAndSettle();
  return vm;
}

void main() {
  late Translations zh;

  setUpAll(() async {
    TestWidgetsFlutterBinding.ensureInitialized();
    zh = await Translations.load(rootBundle, 'zh-CN');
  });

  group('every theme this screen can offer has a name', () {
    test('in both locales, derived from the token artefact', () {
      // **Derived, never listed.** The themes are synced from `frontend-web`, so a
      // hand-typed list here would fall behind the artefact and the symptom would be a
      // radio button labelled `header.theme.something` — which `Translations.t` returns
      // happily, because returning the key is its correct behaviour.
      //
      // **Positive control, measured:** adding `'nameless': {'light': {}, 'dark': {}}`
      // to the token artefact turns this red — and two others with it (the row walk
      // below finds a raw key on screen, and an empty token bag makes light and dark
      // identical). Three reds from one mutation is the shape of a real registry.
      final themes = AppTheme.availableThemes;
      expect(themes, isNotEmpty, reason: 'an empty walk asserts nothing');
      expect(themes, contains(defaultThemeName), reason: 'the fallback must be offerable');
      expect(themes.length, greaterThan(1), reason: 'a one-item radio group is not a choice');

      for (final locale in const ['zh-CN', 'en']) {
        final copy = bundle(locale);
        expect(
          [for (final n in themes) if (!copy.containsKey('header.theme.$n')) n],
          equals(<String>[]),
          reason: locale,
        );
      }
    });

    test('and the list is sorted, so the rows do not reorder between launches', () {
      // `themeTokens` is a map literal; its iteration order is insertion order, which
      // is a property of how somebody typed the file. Sorting makes the screen stable.
      final themes = AppTheme.availableThemes;
      expect(themes, orderedEquals(List.of(themes)..sort()));
    });
  });

  group('the choice is remembered', () {
    test('with nothing stored, the defaults are what was hard-coded before', () async {
      // Somebody upgrading must see no change until they choose. `ink` + dark is
      // exactly what `GewuApp` used to pass literally.
      final repo = SettingsRepository(MemoryPreferencesStore());
      expect(repo.current.value, SettingsRepository.defaults);
      expect(repo.current.value.themeName, defaultThemeName);
      expect(repo.current.value.isDark, isTrue);
    });

    test('a chosen theme survives a restart', () async {
      final store = MemoryPreferencesStore();
      final other = AppTheme.availableThemes.firstWhere((n) => n != defaultThemeName);

      await SettingsRepository(store).setTheme(other);
      // A *new* repository over the same store is what a relaunch is.
      expect(SettingsRepository(store).current.value.themeName, other);
    });

    test('light survives a restart, and so does dark', () async {
      // Both directions. With only the first, an implementation that always restores
      // `false` passes — and `false` is not the default, so it would look deliberate.
      final store = MemoryPreferencesStore();
      await SettingsRepository(store).setDark(false);
      expect(SettingsRepository(store).current.value.isDark, isFalse);

      await SettingsRepository(store).setDark(true);
      expect(SettingsRepository(store).current.value.isDark, isTrue);
    });

    test('a stored theme that no longer exists falls back rather than painting nothing',
        () async {
      // The themes come from the web client. One being deleted there is a real
      // possibility, and the phone would still have the old name written down.
      final store = MemoryPreferencesStore()..values['gewu.theme'] = 'a-theme-from-2024';
      expect(SettingsRepository(store).current.value.themeName, defaultThemeName);
    });

    test('an unknown theme is refused, and refusing does not clear what was there',
        () async {
      final store = MemoryPreferencesStore();
      final repo = SettingsRepository(store);
      final other = AppTheme.availableThemes.firstWhere((n) => n != defaultThemeName);
      await repo.setTheme(other);

      await repo.setTheme('not-a-theme');
      expect(repo.current.value.themeName, other, reason: 'the good choice must stand');
      expect(store.values['gewu.theme'], other);
    });
  });

  group('the two axes are independent', () {
    // **Both directions, because one direction alone is half a test.** The failure this
    // guards against is the flattened model — one setting holding "ink-dark" — where
    // switching to light silently resets the theme and switching theme silently
    // resets to dark. Each direction catches one of those and neither catches both.
    //
    // **Positive control, measured:** making `setDark` also write `defaults.themeName`
    // turns the *second* of these red and leaves the first green — which is the whole
    // argument for writing both. One direction is one bug; it is not this one.
    test('choosing a theme does not change the brightness', () async {
      final repo = SettingsRepository(MemoryPreferencesStore());
      await repo.setDark(false);
      final other = AppTheme.availableThemes.firstWhere((n) => n != defaultThemeName);

      await repo.setTheme(other);
      expect(repo.current.value.isDark, isFalse, reason: 'light must stay light');
      expect(repo.current.value.themeName, other);
    });

    test('changing the brightness does not change the theme', () async {
      final repo = SettingsRepository(MemoryPreferencesStore());
      final other = AppTheme.availableThemes.firstWhere((n) => n != defaultThemeName);
      await repo.setTheme(other);

      await repo.setDark(false);
      expect(repo.current.value.themeName, other, reason: 'the theme must stay chosen');
      expect(repo.current.value.isDark, isFalse);
    });
  });

  group('the choice reaches the paint', () {
    // **This is the half that would have caught the original defect.** Everything above
    // is about a stored string; none of it would notice an app that stores the string
    // and keeps painting `ink`.
    // **Positive control, measured:** `name = defaultThemeName;` at the top of
    // `AppTheme.build` — i.e. exactly the app as it shipped, painting `ink` whatever
    // you chose — turns the first of these red and nothing else in this file. Every
    // assertion about storage stays green, which is precisely the defect.
    test('every theme paints a distinguishable ThemeData', () {
      Object key(String name, Brightness b) {
        final theme = AppTheme.build(name, b);
        return '${theme.colorScheme.primary}/${theme.colorScheme.surface}/'
            '${theme.scaffoldBackgroundColor}';
      }

      for (final b in Brightness.values) {
        final seen = {for (final n in AppTheme.availableThemes) key(n, b)};
        expect(
          seen,
          hasLength(AppTheme.availableThemes.length),
          reason: 'two themes painting identically at $b makes the radio group a lie',
        );
      }
    });

    test('light and dark differ within one theme', () {
      for (final name in AppTheme.availableThemes) {
        expect(
          AppTheme.build(name, Brightness.light).colorScheme.surface,
          isNot(AppTheme.build(name, Brightness.dark).colorScheme.surface),
          reason: name,
        );
      }
    });

    test('the board colour follows the theme, which is why there is no skin axis',
        () {
      // 「棋盘颜色」was asked for as a separate setting. On this client it is not one:
      // `AppTheme.boardBackground` reads `color-well` out of the same token bag, so
      // choosing a theme *is* choosing the board colour. That is a claim about the
      // code, so it is asserted rather than written in a comment and hoped for.
      final backgrounds = {
        for (final n in AppTheme.availableThemes)
          AppTheme.boardBackground(n, Brightness.dark),
      };
      expect(
        backgrounds.length,
        greaterThan(1),
        reason: 'if every theme gave the same board, a skin axis would be the only way',
      );
    });
  });

  group('the screen', () {
    testWidgets('offers one row per theme and none of them shows a raw key',
        (tester) async {
      final tokens = MemoryTokenStore();
      final vm = await pumpSettings(
        tester,
        settings: SettingsRepository(MemoryPreferencesStore()),
        auth: await signedInAuth(tester, tokens),
        strings: zh,
      );

      expect(
        find.byType(RadioListTile<String>),
        findsNWidgets(AppTheme.availableThemes.length),
      );
      for (final name in AppTheme.availableThemes) {
        final label = zh.t('header.theme.$name');
        expect(label, isNot('header.theme.$name'), reason: 'raw key on screen');
        expect(find.text(label), findsOneWidget, reason: name);
      }
      expect(find.byType(SwitchListTile), findsOneWidget, reason: 'the dark toggle');
      vm.dispose();
    });

    testWidgets('tapping a theme changes it, and the screen follows', (tester) async {
      final settings = SettingsRepository(MemoryPreferencesStore());
      final other = AppTheme.availableThemes.firstWhere((n) => n != defaultThemeName);
      final vm = await pumpSettings(
        tester,
        settings: settings,
        auth: await signedInAuth(tester, MemoryTokenStore()),
        strings: zh,
      );
      expect(vm.themeName, defaultThemeName, reason: 'precondition');

      await tester.tap(find.text(zh.t('header.theme.$other')));
      await tester.pumpAndSettle();

      expect(settings.current.value.themeName, other);
      final selected = tester
          .widgetList<RadioListTile<String>>(find.byType(RadioListTile<String>))
          .where((r) => r.value == other);
      expect(selected, hasLength(1));
      // The ViewModel is what the shell reads, so it is what must have moved.
      expect(vm.themeName, other, reason: 'the screen must show the new choice');
      vm.dispose();
    });

    testWidgets('the dark toggle flips both ways', (tester) async {
      final settings = SettingsRepository(MemoryPreferencesStore());
      final vm = await pumpSettings(
        tester,
        settings: settings,
        auth: await signedInAuth(tester, MemoryTokenStore()),
        strings: zh,
      );
      expect(vm.isDark, isTrue, reason: 'precondition — the default is dark');

      await tester.tap(find.byType(SwitchListTile));
      await tester.pumpAndSettle();
      expect(settings.current.value.isDark, isFalse);

      await tester.tap(find.byType(SwitchListTile));
      await tester.pumpAndSettle();
      expect(settings.current.value.isDark, isTrue, reason: 'and back');
      vm.dispose();
    });
  });

  group('signing out asks first', () {
    // 「点击退出没有确认就直接退了」. The precondition on each of these is the point:
    // "we are still signed in" is green for the wrong reason if we were never signed
    // in, and that is exactly the shape of a stub that quietly does nothing.
    testWidgets('cancelling leaves the session alone', (tester) async {
      final tokens = MemoryTokenStore();
      final auth = await signedInAuth(tester, tokens);
      final vm = await pumpSettings(
        tester,
        settings: SettingsRepository(MemoryPreferencesStore()),
        auth: auth,
        strings: zh,
      );
      expect(auth.signedIn.value, isTrue, reason: 'precondition — signed in');

      await tester.tap(find.text(zh.t('header.auth.logout')));
      await tester.pumpAndSettle();
      expect(find.byType(AlertDialog), findsOneWidget, reason: 'it must ask');

      await tester.tap(find.widgetWithText(TextButton, zh.t('lobby.ai-game.cancel')));
      await tester.pumpAndSettle();

      expect(find.byType(AlertDialog), findsNothing);
      expect(auth.signedIn.value, isTrue, reason: 'cancel must not sign out');
      vm.dispose();
    });

    testWidgets('confirming signs out', (tester) async {
      final tokens = MemoryTokenStore();
      final auth = await signedInAuth(tester, tokens);
      final vm = await pumpSettings(
        tester,
        settings: SettingsRepository(MemoryPreferencesStore()),
        auth: auth,
        strings: zh,
      );
      expect(auth.signedIn.value, isTrue, reason: 'precondition — signed in');

      await tester.tap(find.text(zh.t('header.auth.logout')));
      await tester.pumpAndSettle();

      // **Three widgets now carry 「退出登录」**: the row that opened the dialog, the
      // dialog's title, and its confirm button — so scoping to the dialog is not
      // enough either (measured: `find.descendant` still matched two). The button is
      // what a person taps, so the finder names the button.
      //
      // The doubled copy is a constraint, not an oversight: this client may not invent
      // translation keys (`shared_sync_test` forbids a second set), and the shared
      // bundle has no generic 「确定」 and no logout-confirmation body. See the
      // deferral in CLAUDE.md.
      await tester.tap(find.widgetWithText(TextButton, zh.t('header.auth.logout')));
      await tester.pumpAndSettle();

      expect(auth.signedIn.value, isFalse);
      vm.dispose();
    });
  });

  group('the ViewModel is behind the same guard as the others', () {
    test('it extends ViewModel and stops notifying once disposed', () async {
      final settings = SettingsRepository(MemoryPreferencesStore());
      final vm = SettingsViewModel(settings: settings, auth: idleAuth());
      expect(vm, isA<ViewModel>());

      var notifications = 0;
      vm.addListener(() => notifications++);
      await settings.setDark(false);
      expect(notifications, 1, reason: 'a repository change must reach the screen');

      vm.dispose();
      // Non-vacuity for the line below: without the guard this throws in debug, which
      // is the assertion that made `ViewModel` exist in the first place.
      await settings.setDark(true);
      expect(notifications, 1, reason: 'and stops once the screen is gone');
    });
  });
}
