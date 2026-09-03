// Settings, driven through the real shell.
//
//   flutter test integration_test/settings_test.dart -d windows \
//     --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199
//
// **The unit tests cannot see the thing that was actually broken.** They mount
// `SettingsView` on its own; the defect was one level up — `GewuApp` passed literal
// `ink` and `ThemeMode.dark` to `MaterialApp.router`, so a perfectly stored choice
// changed nothing. Only a test that mounts the shell can watch the painted theme move.
//
// It also drives the two other complaints from the same phone session end to end: the
// way *into* settings (there was none) and the confirmation before signing out (there
// was none) — including the redirect that a confirmed sign-out is supposed to trigger,
// which lives in the router and so has no unit test either.
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';

import 'package:gewu_mobile/app.dart';
import 'package:gewu_mobile/data/services/preferences_store.dart';
import 'package:gewu_mobile/data/services/token_store.dart';
import 'package:gewu_mobile/i18n/translations.dart';
import 'package:gewu_mobile/theme/app_theme.dart';
import 'package:gewu_mobile/ui/login/view/login_view.dart';
import 'package:gewu_mobile/ui/settings/view/settings_view.dart';

const server = String.fromEnvironment('GEWU_PROBE_SERVER');

/// Registers through the real form and lands on the catalogue.
Future<AppDependencies> _signIn(
  WidgetTester tester,
  String prefix, {
  required PreferencesStore preferences,
}) async {
  final stamp = DateTime.now().millisecondsSinceEpoch.toString().substring(7);
  final me = '$prefix$stamp'.padRight(20, 'x').substring(0, 20);

  final deps = await AppDependencies.build(
    rootBundle,
    baseUrl: server,
    tokenStore: MemoryTokenStore(),
    preferences: preferences,
  );
  await tester.pumpWidget(GewuApp(deps: deps));
  await tester.pumpAndSettle();

  await tester.tap(find.text(deps.strings.t('auth.login.no-account-cta')));
  await tester.pumpAndSettle();
  final fields = find.byType(TextField);
  await tester.enterText(fields.at(0), '$me@example.com');
  await tester.enterText(fields.at(1), me);
  await tester.enterText(fields.at(2), 'Mobile-pass-1234');
  await tester.pumpAndSettle();
  await tester.tap(find.text(deps.strings.t('auth.register.submit')));
  await tester.pumpAndSettle(const Duration(seconds: 6));

  expect(find.byType(LoginView), findsNothing, reason: 'registration must have worked');
  return deps;
}

/// What `MaterialApp.router` is actually painting right now.
ThemeData _painted(WidgetTester tester) =>
    Theme.of(tester.element(find.byType(Scaffold).first));

/// **Found by its tooltip, not its icon.** `find.byIcon(Icons.settings)` was 0 matches
/// against `Icons.settings_outlined` — and an icon constant is not what a person is
/// looking for anyway. The tooltip is copy, so this also fails if the button loses its
/// label, which is the accessibility half of the same button.
Future<void> _openSettings(WidgetTester tester, Translations t) async {
  await tester.tap(find.byTooltip(t.t('header.settings.label')));
  await tester.pumpAndSettle(const Duration(seconds: 2));
  expect(find.byType(SettingsView), findsOneWidget);
}

/// Exactly what Android's back gesture sends.
Future<void> systemBack(WidgetTester tester) async {
  await tester.binding.defaultBinaryMessenger.handlePlatformMessage(
    'flutter/navigation',
    const JSONMethodCodec().encodeMethodCall(const MethodCall('popRoute')),
    (_) {},
  );
  await tester.pumpAndSettle(const Duration(seconds: 2));
}

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  if (server.isEmpty) {
    test(
      'settings need a live backend to sign in against',
      () {},
      skip: 'set --dart-define=GEWU_PROBE_SERVER=http://127.0.0.1:5199; it is NOT set now',
    );
    return;
  }

  testWidgets('choosing a theme repaints the whole app, and back returns to the catalogue',
      (tester) async {
    final prefs = MemoryPreferencesStore();
    final deps = await _signIn(tester, 'st', preferences: prefs);
    final t = deps.strings;

    // --- there is a way in, and it sits ON the catalogue ----------------------
    expect(
      Navigator.of(tester.element(find.text(t.t('catalog.title')))).canPop(),
      isFalse,
      reason: 'precondition: the catalogue is the bottom of the stack',
    );
    await _openSettings(tester, t);
    expect(
      Navigator.of(tester.element(find.byType(SettingsView))).canPop(),
      isTrue,
      reason: 'settings must nest under /, or back would exit the app',
    );

    // --- the painted theme, before and after ---------------------------------
    final before = _painted(tester);
    expect(deps.settings.current.value.themeName, defaultThemeName, reason: 'precondition');

    final other = AppTheme.availableThemes.firstWhere((n) => n != defaultThemeName);
    await tester.tap(find.text(t.t('header.theme.$other')));
    await tester.pumpAndSettle(const Duration(seconds: 2));

    final after = _painted(tester);
    expect(deps.settings.current.value.themeName, other, reason: 'the choice must land');
    expect(
      after.colorScheme.primary,
      isNot(before.colorScheme.primary),
      reason: 'THE defect: the app painted `ink` whatever was stored',
    );
    // And it is the theme the artefact says it is, not merely *a* different one.
    expect(
      after.colorScheme.primary,
      AppTheme.build(other, Brightness.dark).colorScheme.primary,
    );

    // --- dark is the other axis, and it moves on its own ----------------------
    expect(before.brightness, Brightness.dark, reason: 'precondition: the default is dark');
    // **By label, not by type.** There are two switches on this screen now (dark and
    // sound), so `find.byType(SwitchListTile)` stopped being unambiguous — the same
    // shape as the lobby's second FAB, and it went red here for exactly that reason.
    await tester.tap(find.widgetWithText(SwitchListTile, t.t('header.theme.dark-toggle')));
    await tester.pumpAndSettle(const Duration(seconds: 2));
    expect(_painted(tester).brightness, Brightness.light);
    expect(
      deps.settings.current.value.themeName,
      other,
      reason: 'and switching brightness must not have reset the theme',
    );

    // --- and sound is a third axis, under the real shell ----------------------
    expect(deps.settings.current.value.soundOn, isTrue, reason: 'precondition — on');
    await tester.tap(find.widgetWithText(SwitchListTile, t.t('header.sound.label')));
    await tester.pumpAndSettle(const Duration(seconds: 2));
    expect(deps.settings.current.value.soundOn, isFalse);
    expect(
      deps.settings.current.value.themeName,
      other,
      reason: 'muting must not have reset the theme',
    );
    expect(_painted(tester).brightness, Brightness.light, reason: 'nor the brightness');

    // --- every choice is written down ----------------------------------------
    expect(prefs.values['gewu.theme'], other);
    expect(prefs.values['gewu.dark'], 'false');
    expect(prefs.values['gewu.sound'], 'false');

    // --- back lands on the catalogue, not on the login page -------------------
    await systemBack(tester);
    expect(find.text(t.t('catalog.title')), findsOneWidget);
    expect(find.byType(LoginView), findsNothing);
    // The catalogue is painted by the same shell, so it kept the new theme.
    expect(_painted(tester).brightness, Brightness.light);
  });

  testWidgets('signing out asks, and only a confirmed one redirects', (tester) async {
    final deps = await _signIn(tester, 'so', preferences: MemoryPreferencesStore());
    final t = deps.strings;
    await _openSettings(tester, t);

    expect(deps.auth.signedIn.value, isTrue, reason: 'precondition — signed in');

    // --- cancel ---------------------------------------------------------------
    await tester.tap(find.text(t.t('header.auth.logout')));
    await tester.pumpAndSettle();
    expect(find.byType(AlertDialog), findsOneWidget, reason: 'it must ask first');
    await tester.tap(find.widgetWithText(TextButton, t.t('lobby.ai-game.cancel')));
    await tester.pumpAndSettle(const Duration(seconds: 2));

    expect(deps.auth.signedIn.value, isTrue, reason: 'cancel must not sign out');
    expect(find.byType(SettingsView), findsOneWidget, reason: 'and must not navigate');

    // --- confirm --------------------------------------------------------------
    await tester.tap(find.text(t.t('header.auth.logout')));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(TextButton, t.t('header.auth.logout')));
    await tester.pumpAndSettle(const Duration(seconds: 4));

    expect(deps.auth.signedIn.value, isFalse);
    // **Nothing in `confirmSignOut` navigates.** Landing here is the router's
    // `redirect` reacting to `signedIn`, which is the only place that decision lives.
    expect(find.byType(LoginView), findsOneWidget, reason: 'the redirect must take it');
  });
}
