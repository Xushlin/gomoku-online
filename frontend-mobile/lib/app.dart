/// App shell: theme, translations, and which screen is showing.
library;

import 'package:flutter/material.dart';

import 'api/api_client.dart';
import 'api/secure_token_store.dart';
import 'config/server.dart';
import 'i18n/translations.dart';
import 'screens/game_screen.dart';
import 'screens/lobby_screen.dart';
import 'screens/login_screen.dart';
import 'theme/app_theme.dart';

/// Everything the screens need, passed down explicitly.
///
/// No state-management package for one slice of one client: a container that is
/// handed down is easier to read than a locator, and it makes "what does this screen
/// depend on" answerable from its constructor.
class AppServices {
  AppServices({required this.api, required this.strings, required this.tokens});

  final ApiClient api;
  final Translations strings;
  final TokenStore tokens;

  String? username;
}

class GewuApp extends StatefulWidget {
  const GewuApp({super.key, required this.services});

  final AppServices services;

  @override
  State<GewuApp> createState() => _GewuAppState();
}

class _GewuAppState extends State<GewuApp> {
  bool _authenticated = false;
  String? _openRoomId;

  @override
  void initState() {
    super.initState();
    _tryResume();
  }

  /// A stored refresh token means "log in silently".
  Future<void> _tryResume() async {
    final ok = await widget.services.api.refresh();
    if (mounted && ok) setState(() => _authenticated = true);
  }

  @override
  Widget build(BuildContext context) {
    final services = widget.services;

    return MaterialApp(
      title: 'Gewu',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.build(defaultThemeName, Brightness.light),
      darkTheme: AppTheme.build(defaultThemeName, Brightness.dark),
      themeMode: ThemeMode.dark,
      home: switch ((_authenticated, _openRoomId)) {
        (false, _) => LoginScreen(
          services: services,
          onSignedIn: () => setState(() => _authenticated = true),
        ),
        (true, final String roomId) => GameScreen(
          services: services,
          roomId: roomId,
          onLeave: () => setState(() => _openRoomId = null),
        ),
        (true, null) => LobbyScreen(
          services: services,
          onOpenRoom: (id) => setState(() => _openRoomId = id),
          onSignedOut: () => setState(() {
            _authenticated = false;
            _openRoomId = null;
          }),
        ),
      },
    );
  }
}

/// Builds the service graph. Kept out of `main` so a test can construct it too.
Future<AppServices> bootstrap(AssetBundle bundle, {String locale = 'zh-CN'}) async {
  final tokens = SecureTokenStore();
  return AppServices(
    api: ApiClient(baseUrl: serverAddress, tokens: tokens),
    strings: await Translations.load(bundle, locale),
    tokens: tokens,
  );
}
