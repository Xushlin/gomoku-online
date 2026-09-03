/// App shell: the Provider graph, the theme, and which screen is showing.
library;

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'config/server.dart';
import 'data/repositories/auth_repository.dart';
import 'data/repositories/game_catalog_repository.dart';
import 'data/repositories/settings_repository.dart';
import 'data/repositories/sound_repository.dart';
import 'data/repositories/room_repository.dart';
import 'data/services/dio_client.dart';
import 'data/services/match_hub_service.dart';
import 'data/services/preferences_store.dart';
import 'data/services/sound_player.dart';
import 'data/services/token_store.dart';
import 'i18n/translations.dart';
import 'theme/app_theme.dart';
import 'ui/router.dart';

/// Wires services -> repositories -> view models.
///
/// Built here rather than in `main` so a test can construct the same graph against a
/// different server or a fake token store.
class AppDependencies {
  AppDependencies._({
    required this.auth,
    required this.rooms,
    required this.catalog,
    required this.settings,
    required this.sound,
    required this.strings,
    required this.tokens,
  });

  final AuthRepository auth;
  final RoomRepository rooms;
  final GameCatalogRepository catalog;
  final SettingsRepository settings;
  final SoundRepository sound;
  final Translations strings;
  final TokenStore tokens;

  static Future<AppDependencies> build(
    AssetBundle bundle, {
    String locale = 'zh-CN',
    String? baseUrl,
    TokenStore? tokenStore,
    PreferencesStore? preferences,
    SoundPlayer? soundPlayer,
  }) async {
    final tokens = tokenStore ?? SecureTokenStore();
    final address = baseUrl ?? serverAddress;

    // The refresh call goes through this same client, so it is injected as a
    // callback rather than a constructor argument — otherwise the wiring is circular.
    final settings = SettingsRepository(preferences ?? await SharedPreferencesStore.open());

    late final AuthRepository auth;
    final dio = buildDio(
      baseUrl: address,
      tokens: tokens,
      refresh: () => auth.refresh(),
    );
    auth = AuthRepository(dio: dio, tokens: tokens);

    final hub = MatchHub(
      serverAddress: address,
      accessToken: () => tokens.access ?? '',
    );

    return AppDependencies._(
      auth: auth,
      rooms: RoomRepository(dio: dio, hub: hub),
      catalog: GameCatalogRepository(dio),
      settings: settings,
      sound: SoundRepository(
        player: soundPlayer ?? AudioPlayersSoundPlayer(),
        settings: settings,
      ),
      strings: await Translations.load(bundle, locale),
      tokens: tokens,
    );
  }
}

/// The shell.
///
/// **`StatelessWidget`, and that is the mechanism rather than tidiness.** It used to
/// be a `StatefulWidget` holding `_authenticated` and `_openRoomId` and switching on
/// the pair — so "which screen" lived in two booleans that nothing enforced, no screen
/// was a route, and the system back button had nothing to pop. A `StatelessWidget`
/// cannot reach `setState`, so that state now has nowhere to hide: the compiler keeps
/// this honest, not the next reader.
class GewuApp extends StatelessWidget {
  /// The router is built once, in the initializer list.
  ///
  /// **Not inside `build`**: a `GoRouter` owns the navigation stack, so rebuilding one
  /// per frame would throw the history away on every repaint.
  GewuApp({super.key, required this.deps}) : router = buildRouter(deps);

  final AppDependencies deps;
  final GoRouter router;

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        Provider<Translations>.value(value: deps.strings),
        Provider<AuthRepository>.value(value: deps.auth),
        Provider<RoomRepository>.value(value: deps.rooms),
        Provider<GameCatalogRepository>.value(value: deps.catalog),
        Provider<SettingsRepository>.value(value: deps.settings),
        Provider<SoundRepository>.value(value: deps.sound),
      ],
      // **A listener, not state on the shell.** `GewuApp` has to stay a
      // `StatelessWidget` — `test/shell_state_test.dart` pins that with a tear-off the
      // compiler checks — so the rebuild on a theme change hangs off the settings value
      // rather than moving "which theme" back into the widget.
      child: ValueListenableBuilder<AppSettings>(
        valueListenable: deps.settings.current,
        builder: (context, settings, _) => MaterialApp.router(
          title: 'Gewu',
          debugShowCheckedModeBanner: false,
          // Two orthogonal axes: the theme names the palette, `themeMode` picks the
          // half of it to use. Flattening them into one list is how switching to dark
          // starts resetting the theme.
          theme: AppTheme.build(settings.themeName, Brightness.light),
          darkTheme: AppTheme.build(settings.themeName, Brightness.dark),
          themeMode: settings.isDark ? ThemeMode.dark : ThemeMode.light,
          routerConfig: router,
        ),
      ),
    );
  }
}
