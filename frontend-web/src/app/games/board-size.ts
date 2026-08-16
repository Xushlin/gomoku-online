import type { GameCapabilitiesService } from './game-capabilities.service';

/**
 * Gomoku's board, used when a game key cannot be resolved.
 *
 * Falling back rather than throwing is deliberate: a client that has not been
 * redeployed will meet game keys the server describes and it does not, and the
 * right failure mode there is a board that may be the wrong size — not a blank
 * page. The server rejects out-of-range moves regardless, so a wrong guess here
 * cannot corrupt a game.
 *
 * It is **not** meant to cover "the descriptor has not arrived yet". Callers
 * hold their loading state until `capabilities.loaded()`, so a player never sees
 * this value stand in for a size the client is about to learn.
 */
export const DEFAULT_BOARD = { rows: 15, cols: 15 } as const;

/** Board dimensions for a game key, or {@link DEFAULT_BOARD} when unknown. */
export interface BoardSize {
  readonly rows: number;
  readonly cols: number;
}

/**
 * Resolve a room's board dimensions from the server's game descriptors.
 *
 * The source of truth is the backend's `IGameRules`; `GET /api/games` puts it on
 * the wire and {@link GameCapabilitiesService} caches it. This used to read
 * `GameManifest.board` — a client-side copy of that same data, tolerated because
 * a wrong copy showed up as a visibly wrong number of cells. `add-web-xiangqi`
 * ended that argument by adding a copy **nothing reads**: 象棋's board component
 * hardcodes its own 10×9, so a wrong value there would be noticed by nobody.
 *
 * Lives here rather than in either page so the fallback exists once: the room
 * page and the replay page need the identical answer, and two copies of a
 * fallback are two chances to disagree about it.
 *
 * @param capabilities Server-declared game descriptors.
 * @param gameKey The room's game key; `null` / `undefined` while state loads.
 */
export function boardSizeFor(
  capabilities: GameCapabilitiesService,
  gameKey: string | null | undefined,
): BoardSize {
  if (!gameKey) return DEFAULT_BOARD;
  const descriptor = capabilities.of(gameKey);
  if (!descriptor || descriptor.rows <= 0 || descriptor.cols <= 0) return DEFAULT_BOARD;
  return { rows: descriptor.rows, cols: descriptor.cols };
}
