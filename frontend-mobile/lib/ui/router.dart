/// The route table. **The only thing that decides which screen is on.**
library;

import 'package:flutter/widgets.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../app.dart';
import 'game/view/game_view.dart';
import 'game/view_model/game_view_model.dart';
import 'lobby/view/lobby_view.dart';
import 'lobby/view_model/lobby_view_model.dart';
import 'login/view/login_view.dart';
import 'login/view_model/login_view_model.dart';

/// Routes, as paths.
const loginRoute = '/login';
const lobbyRoute = '/';
String roomRoute(String id) => '/rooms/$id';

/// Builds the router.
///
/// **`rooms/:id` is nested under `/`, and that nesting is the whole point.** go_router
/// builds the navigator stack out of the matched route *hierarchy*: a top-level
/// `/rooms/:id` would replace the stack, so `canPop()` in a room would be false and
/// the system back button would still exit the app — which is exactly the defect this
/// route table exists to fix. Measured before the fix: `canPop()` was **false** in a
/// room, and a `popRoute` left you in the room.
GoRouter buildRouter(AppDependencies deps) => GoRouter(
  initialLocation: lobbyRoute,

  // A value, not an event: see `AuthRepository.signedIn`.
  //
  // **This carries every auth transition, not just expiry.** Measured by deleting it:
  // all three router integration tests go red at *login*, because nothing in
  // `LoginView` navigates any more — a successful sign-in reaches the lobby only
  // because this re-runs `redirect`. The prediction when the mutation was written was
  // "the dead-session test goes red"; the mechanism is wider than that.
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
      path: lobbyRoute,
      builder: (context, state) => ChangeNotifierProvider(
        create: (_) => LobbyViewModel(rooms: deps.rooms, auth: deps.auth),
        child: const LobbyView(),
      ),
      routes: [
        GoRoute(
          path: 'rooms/:id',
          builder: (context, state) {
            final id = state.pathParameters['id']!;
            return ChangeNotifierProvider(
              // Keyed by room id: opening a different room must build a fresh view
              // model rather than reuse one still pointed at the previous room.
              key: ValueKey(id),
              create: (_) => GameViewModel(rooms: deps.rooms, roomId: id),
              child: const GameView(),
            );
          },
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
  if (atLogin) return lobbyRoute;
  return null;
}
