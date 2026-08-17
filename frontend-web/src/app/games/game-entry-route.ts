import type { GameCatalogService } from './game-catalog.service';

/** Where "out of here" goes when there is no game to go back to. */
export const PLATFORM_HOME = '/home';

/**
 * Where to send a player leaving a game — its own entry point.
 *
 * Before `generalize-lobby` every exit went to `/home`, because `/home` *was*
 * gomoku's lobby. It is now the platform home, so finishing a 象棋 match and
 * landing there means landing on a page with no trace of what you were playing.
 *
 * The manifest already answers this: `generalize-lobby` set gomoku's
 * `launchRoute` to `/g/gomoku/lobby`, so the entry point and the lobby are the
 * same route for games that have one. Games that do not — 一字棋, 象棋 — get
 * their human-vs-AI page, which is where you start another one. Their entry
 * page *is* their lobby; it just is not a room list.
 *
 * Reads the **catalogue**, not `GameCapabilitiesService`. There is no
 * `supportsHumanVsHuman` branch to make, and the catalogue is a static import:
 * synchronous, never failing, never empty. Consulting the async descriptors
 * would add a loading gate to a code path that navigates away from the page.
 *
 * @param catalog The client-side game manifests.
 * @param gameKey The room's or replay's game key; `null` when it never loaded.
 */
export function gameEntryRoute(
  catalog: GameCatalogService,
  gameKey: string | null | undefined,
): string {
  if (!gameKey) return PLATFORM_HOME;
  return catalog.byKey(gameKey)?.launchRoute ?? PLATFORM_HOME;
}
