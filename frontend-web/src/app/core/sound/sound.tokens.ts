/**
 * Sound layer contracts.
 *
 * `SOUND_EVENTS` is the single source: the closed set of UX events the rest of
 * the app may emit, and `SoundEventName` is derived from it. Declaring the array
 * and the union separately is the defect this repo has fixed four times — a
 * hand-written list that a test walking "everything" believes is everything.
 * Derived, the runtime list cannot go stale.
 *
 * The set is **platform-wide, not per-game**: a game plays the subset it needs.
 * `capture` never fires in 俄罗斯方块 and `line-clear` never fires in 象棋, and
 * nothing has to enforce that.
 *
 * `SoundPack` is the shape of a pluggable audio "skin". A pack is given the
 * shared `AudioContext` and master `GainNode` and is expected to construct a
 * short-lived audio graph that auto-stops via `node.stop(when)`. Packs MUST be
 * synchronous (fire-and-forget — the browser schedules the actual playback) and
 * MUST NOT throw.
 */
export const SOUND_EVENTS = [
  /** A move landed — a stone placed, a piece moved, a tetromino locked. */
  'move-place',
  /** A move took an enemy piece. 象棋 only; the client already knows. */
  'capture',
  /** 1–3 rows cleared at once. */
  'line-clear',
  /** Four rows at once — deliberately not four times a single row, in points or in sound. */
  'line-clear-quad',
  /** The level advanced, which in 俄罗斯方块 means gravity just got faster. */
  'level-up',
  /**
   * A hand was dealt. 斗地主 only so far.
   *
   * Fires on the deal *arriving*, not on the page rendering one: a reload paints
   * the dealing animation again (new DOM nodes, so the CSS runs), and that is
   * decoration — but replaying the sound would report an event that did not
   * happen. See `RoomPage.previousHandCount`.
   */
  'card-deal',
  /**
   * Cards were played onto the table.
   *
   * Distinct from `move-place`, which every other game's move uses and which
   * 斗地主's **pass** and **bid** keep — so a player can hear the difference
   * between someone playing and someone passing without looking.
   */
  'card-play',
  'game-win',
  'game-lose',
  'game-draw',
  'urge',
] as const;

export type SoundEventName = (typeof SOUND_EVENTS)[number];

export interface SoundPack {
  readonly play: (event: SoundEventName, ctx: AudioContext, masterGain: GainNode) => void;
}

/**
 * Terminal `default:` arm of a pack's `switch`, and the mechanism that makes a
 * pack unable to forget an event.
 *
 * The comment that used to sit at the top of this file claimed TypeScript
 * already did this: "adding a sixth requires editing this union (TS
 * exhaustiveness then forces every registered pack to render it — or fall
 * through silently)". Those two halves contradict each other, and **the second
 * one was the true one**. Measured: a sixth member added to the union with all
 * three packs untouched compiles clean, `tsc --noEmit -p tsconfig.app.json`
 * exit 0 — because `play` returns `void`, so a missing `case` just runs off the
 * end. A deliberate type error in `wood.ts` produced four errors from the same
 * command, so the compiler was certainly reading those files.
 *
 * With `event: never`, a missing `case` leaves `event` narrowed to the forgotten
 * member here, and the call fails to compile *naming it*.
 *
 * It is a silent no-op at runtime rather than a throw, because packs MUST NOT
 * throw (the service would swallow it anyway) — and the compiler has already
 * proven this line unreachable.
 */
export function unhandledSoundEvent(event: never): void {
  void event;
}
