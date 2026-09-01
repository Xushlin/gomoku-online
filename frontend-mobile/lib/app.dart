/// App shell: the Provider graph, the theme, and which screen is showing.
library;

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'config/server.dart';
import 'data/repositories/auth_repository.dart';
import 'data/repositories/room_repository.dart';
import 'data/services/dio_client.dart';
import 'data/services/match_hub_service.dart';
import 'data/services/token_store.dart';
import 'i18n/translations.dart';
import 'theme/app_theme.dart';
import 'ui/game/view/game_view.dart';
import 'ui/game/view_model/game_view_model.dart';
import 'ui/lobby/view/lobby_view.dart';
import 'ui/lobby/view_model/lobby_view_model.dart';
import 'ui/login/view/login_view.dart';
import 'ui/login/view_model/login_view_model.dart';

/// Wires services -> repositories -> view models.
///
/// Built here rather than in `main` so a test can construct the same graph against a
/// different server or a fake token store.
class AppDependencies {
  AppDependencies._({
    required this.auth,
    required this.rooms,
    required this.strings,
    required this.tokens,
  });

  final AuthRepository auth;
  final RoomRepository rooms;
  final Translations strings;
  final TokenStore tokens;

  static Future<AppDependencies> build(
    AssetBundle bundle, {
    String locale = 'zh-CN',
    String? baseUrl,
    TokenStore? tokenStore,
  }) async {
    final tokens = tokenStore ?? SecureTokenStore();
    final address = baseUrl ?? serverAddress;

    // The refresh call goes through this same client, so it is injected as a
    // callback rather than a constructor argument — otherwise the wiring is circular.
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
      strings: await Translations.load(bundle, locale),
      tokens: tokens,
    );
  }
}

class GewuApp extends StatefulWidget {
  const GewuApp({super.key, required this.deps});

  final AppDependencies deps;

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
    final ok = await widget.deps.auth.refresh();
    if (mounted && ok) setState(() => _authenticated = true);
  }

  @override
  Widget build(BuildContext context) {
    final deps = widget.deps;

    return MultiProvider(
      providers: [
        Provider<Translations>.value(value: deps.strings),
        Provider<AuthRepository>.value(value: deps.auth),
        Provider<RoomRepository>.value(value: deps.rooms),
      ],
      child: MaterialApp(
        title: 'Gewu',
        debugShowCheckedModeBanner: false,
        theme: AppTheme.build(defaultThemeName, Brightness.light),
        darkTheme: AppTheme.build(defaultThemeName, Brightness.dark),
        themeMode: ThemeMode.dark,
        home: Builder(
          builder: (context) => switch ((_authenticated, _openRoomId)) {
            (false, _) => ChangeNotifierProvider(
              create: (_) => LoginViewModel(deps.auth),
              child: LoginView(onSignedIn: () => setState(() => _authenticated = true)),
            ),
            (true, final String roomId) => ChangeNotifierProvider(
              // Keyed by room id so opening a different room builds a fresh view
              // model rather than reusing one pointed at the previous room.
              key: ValueKey(roomId),
              create: (_) => GameViewModel(rooms: deps.rooms, roomId: roomId),
              child: GameView(onLeave: () => setState(() => _openRoomId = null)),
            ),
            (true, null) => ChangeNotifierProvider(
              create: (_) => LobbyViewModel(rooms: deps.rooms, auth: deps.auth),
              child: LobbyView(
                onOpenRoom: (id) => setState(() => _openRoomId = id),
                onSignedOut: () async {
                  await deps.auth.logout();
                  if (mounted) {
                    setState(() {
                      _authenticated = false;
                      _openRoomId = null;
                    });
                  }
                },
              ),
            ),
          },
        ),
      ),
    );
  }
}
