/// The route table. **The only thing that decides which screen is on.**
library;

import 'package:flutter/widgets.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../app.dart';
import 'catalog/view/catalog_view.dart';
import 'catalog/view_model/catalog_view_model.dart';
import 'game/view/game_view.dart';
import 'game/view_model/game_view_model.dart';
import 'lobby/view/lobby_view.dart';
import 'lobby/view_model/lobby_view_model.dart';
import 'login/view/login_view.dart';
import 'login/view_model/login_view_model.dart';

/// Routes, as paths.
const loginRoute = '/login';
const catalogRoute = '/';
String lobbyRouteFor(String gameKey) => '/games/$gameKey';
String roomRouteFor(String gameKey, String roomId) => '/games/$gameKey/rooms/$roomId';

/// Builds the router.
///
/// **The three signed-in routes are nested, and that nesting is the whole point.**
/// go_router builds the navigator stack out of the matched route *hierarchy*: a
/// top-level `/games/:key` would replace the stack instead of sitting on the
/// catalogue, so `canPop()` would be false and the system back button would exit the
/// app. Measured in `add-mobile-router` by doing exactly that — it compiled, analysed
/// clean, `redirect` kept working, and `AppBar` drew no back button at all.
GoRouter buildRouter(AppDependencies deps) => GoRouter(
  initialLocation: catalogRoute,

  // A value, not an event: see `AuthRepository.signedIn`.
  //
  // **This carries every auth transition, not just expiry.** Measured by deleting it:
  // all three router integration tests go red at *login*, because nothing in
  // `LoginView` navigates any more — a successful sign-in reaches the catalogue only
  // because this re-runs `redirect`.
  refreshListenable: deps.auth.signedIn,

  redirect: (context, state) => redirectFor(
    signedIn: deps.auth.signedIn.value,
    location: state.matchedLocation,
  ),

  routes: [
    GoRoute(
      path: loginRoute,
      builder: (context, state) => ChangeNotifierProvider(
        create: (_) => LoginViewModel(deps.auth),
        child: const LoginView(),
      ),
    ),
    GoRoute(
      path: catalogRoute,
      builder: (context, state) => ChangeNotifierProvider(
        create: (_) => CatalogViewModel(catalog: deps.catalog, auth: deps.auth),
        child: const CatalogView(),
      ),
      routes: [
        GoRoute(
          path: 'games/:key',
          builder: (context, state) {
            final gameKey = state.pathParameters['key']!;
            return ChangeNotifierProvider(
              // Keyed by game: switching games must build a fresh view model rather
              // than reuse one still listing the previous game's rooms.
              key: ValueKey(gameKey),
              create: (_) => LobbyViewModel(
                rooms: deps.rooms,
                auth: deps.auth,
                catalog: deps.catalog,
                gameKey: gameKey,
              ),
              child: const LobbyView(),
            );
          },
          routes: [
            GoRoute(
              path: 'rooms/:id',
              builder: (context, state) {
                final roomId = state.pathParameters['id']!;
                return ChangeNotifierProvider(
                  // Keyed by room id: opening a different room must build a fresh view
                  // model rather than reuse one pointed at the previous room.
                  key: ValueKey(roomId),
                  create: (_) => GameViewModel(
                    rooms: deps.rooms,
                    catalog: deps.catalog,
                    auth: deps.auth,
                    roomId: roomId,
                  ),
                  child: const GameView(),
                );
              },
            ),
          ],
        ),
      ],
    ),
  ],
);

/// Where a request for [location] should actually go, or null to let it through.
///
/// **Pulled out as a pure function so both directions can be walked exhaustively.**
/// One place decides this. Before the route table, `_authenticated` only ever went
/// false in the lobby's logout button, so an expired refresh token left you sitting on
/// the lobby looking at an error toast — measured: `at-login=false`,
/// `still-at-lobby=true`.
///
/// **Both directions are rules, not one rule and a nicety.** With only the first, an
/// implementation that redirects *everything* to `/login` passes.
String? redirectFor({required bool signedIn, required String location}) {
  final atLogin = location == loginRoute;

  if (!signedIn) return atLogin ? null : loginRoute;
  if (atLogin) return catalogRoute;
  return null;
}
