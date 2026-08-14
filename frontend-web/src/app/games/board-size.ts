import type { GameCatalogService } from './game-catalog.service';

/**
 * Gomoku's board, used when a game key cannot be resolved.
 *
 * Falling back rather than throwing is deliberate: a client that has not been
 * redeployed will meet game keys its registry does not know, and the right
 * failure mode there is a board that may be the wrong size — not a blank page.
 * The server rejects out-of-range moves regardless, so a wrong guess here cannot
 * corrupt a game.
 */
export const DEFAULT_BOARD = { rows: 15, cols: 15 } as const;

/** Board dimensions for a game key, or {@link DEFAULT_BOARD} when unknown. */
export interface BoardSize {
  readonly rows: number;
  readonly cols: number;
}

/**
 * Resolve a room's board dimensions from the game registry.
 *
 * Lives here rather than in either page so the fallback exists once: the room
 * page and the replay page need the identical answer, and two copies of a
 * fallback are two chances to disagree about it.
 *
 * @param catalog Game registry.
 * @param gameKey The room's game key; `null` / `undefined` while state loads.
 */
export function boardSizeFor(
  catalog: GameCatalogService,
  gameKey: string | null | undefined,
): BoardSize {
  if (!gameKey) return DEFAULT_BOARD;
  return catalog.byKey(gameKey)?.board ?? DEFAULT_BOARD;
}
