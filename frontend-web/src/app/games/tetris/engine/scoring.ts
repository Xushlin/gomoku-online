/** Lines needed per level. */
export const LINES_PER_LEVEL = 10;

/**
 * Base score for clearing 1–4 rows. Four rows is deliberately not four times
 * one row — that difference is the whole "save up for a tetris" decision.
 */
const LINE_SCORES = [0, 100, 300, 500, 800] as const;

/** Level from total lines cleared: one per {@link LINES_PER_LEVEL}, from 1. */
export function levelFor(lines: number): number {
  return Math.floor(lines / LINES_PER_LEVEL) + 1;
}

/**
 * Score for one clear. A port of the backend's public `ScoreForClear`.
 *
 * It is public on the server because the formula is part of the external
 * contract (a leaderboard has to be understandable) and the client shows
 * per-clear scores. That second reason is this file.
 *
 * @param clearedLines How many rows this clear took, 1–4.
 * @param linesBefore Total cleared *before* this one — the level comes from it,
 *   so a clear scores at the level the player was on when they made it.
 */
export function scoreForClear(clearedLines: number, linesBefore: number): number {
  if (clearedLines < 1 || clearedLines > 4) {
    throw new RangeError(`clearedLines must be 1–4, was ${clearedLines}`);
  }
  return LINE_SCORES[clearedLines] * levelFor(linesBefore);
}
