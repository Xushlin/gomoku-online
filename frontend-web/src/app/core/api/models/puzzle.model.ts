/**
 * Puzzle DTO shapes — mirror the backend's `Gewu.Application.Common.DTOs.PuzzleDtos`.
 * JSON serialiser: System.Text.Json with camelCase naming.
 *
 * Note what is absent: nothing here can carry a solution. That is the
 * answer-key confinement rule from `puzzle-core`, and it is enforced on the
 * server by the same absence.
 */

/** One level in the list, with the caller's own best result. */
export interface PuzzleLevelSummary {
  readonly levelIndex: number;
  readonly difficulty: number;
  readonly unlocked: boolean;
  readonly bestStars: number | null;
  readonly bestDurationMs: number | null;
}

/** A level's playable content. `layoutJson` is opaque to the platform. */
export interface PuzzleLevelDetail {
  readonly levelIndex: number;
  readonly difficulty: number;
  readonly layoutJson: string;
}

/** Returned when an attempt starts. */
export interface PuzzleAttemptStarted {
  readonly attemptId: string;
  readonly levelIndex: number;
  readonly layoutJson: string;
  readonly startedAt: string;
}

/** Raw check response. `payloadJson` is a JSON string *inside* the JSON. */
export interface PuzzleCheckResultDto {
  readonly isCorrect: boolean;
  readonly mistakes: number;
  readonly payloadJson: string | null;
}

/** Raw hint response. `revealedJson` is likewise a nested JSON string. */
export interface PuzzleHintDto {
  readonly revealedJson: string;
  readonly hintsUsed: number;
}

/** Submit response. */
export interface PuzzleSubmitResult {
  readonly isCorrect: boolean;
  readonly stars: number | null;
  readonly durationMs: number | null;
  readonly mistakes: number;
  readonly hintsUsed: number;
  readonly newBest: boolean;
}

/** Derived progress for a game. Both numbers are computed server-side per request. */
export interface PuzzleProgress {
  readonly gameKey: string;
  readonly unlockedLevelIndex: number;
  readonly totalStars: number;
  readonly levelsCompleted: number;
}

// ---- 成语纵横 payload shapes (inside `layoutJson` / `payloadJson` / `revealedJson`) ----

/** Direction of an idiom in the grid. */
export type CrosswordDirection = 'Horizontal' | 'Vertical';

/** A grid coordinate. */
export interface CrosswordCell {
  readonly row: number;
  readonly col: number;
}

/** A pre-filled cell — deliberately reveals its own character. */
export interface CrosswordGivenCell {
  readonly row: number;
  readonly col: number;
  readonly char: string;
}

/**
 * One idiom's run of cells. Carries no idiom — the client learns which cells
 * belong together so it knows when a slot is full and worth checking.
 */
export interface CrosswordSlot {
  readonly index: number;
  readonly row: number;
  readonly col: number;
  readonly direction: CrosswordDirection;
  readonly length: number;
}

/** The playable layout. Contains no answers. */
export interface CrosswordLayout {
  readonly rows: number;
  readonly cols: number;
  readonly cells: readonly CrosswordCell[];
  readonly given: readonly CrosswordGivenCell[];
  readonly tray: readonly string[];
  readonly slots: readonly CrosswordSlot[];
}

/** Returned on a correct `check` — the idiom the player just solved, plus its gloss. */
export interface CrosswordSolvedWord {
  readonly index: number;
  readonly word: string;
  readonly explanation: string;
}

/** Returned by `hint` — exactly one cell. */
export interface CrosswordRevealedCell {
  readonly row: number;
  readonly col: number;
  readonly char: string;
}

/** Parsed check result — `solved` is non-null only on a correct verdict. */
export interface PuzzleCheckResult {
  readonly isCorrect: boolean;
  readonly mistakes: number;
  readonly solved: CrosswordSolvedWord | null;
}

/** Parsed hint result. */
export interface PuzzleHint {
  readonly revealed: CrosswordRevealedCell | null;
  readonly hintsUsed: number;
}

/**
 * What the client tells the server about its own board when asking for a hint.
 *
 * Contains no answers — only which cells hold a character and where the cursor
 * is, both of which the player can already see. The server keeps the only copy
 * of the solution and still returns exactly one cell.
 */
export interface CrosswordHintState {
  readonly filled: readonly string[];
  readonly selected: string | null;
}
