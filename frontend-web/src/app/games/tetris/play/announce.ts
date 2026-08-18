import type { SoundEventName } from '../../../core/sound/sound.tokens';

/** What the component observes about a run, between two gravity steps. */
export interface TetrisProgress {
  /** Placements recorded so far — one per locked piece. */
  readonly locks: number;
  readonly lines: number;
  readonly level: number;
  readonly over: boolean;
}

/**
 * Which single sound a gravity step earned, if any.
 *
 * ### Why the engine does not do this
 *
 * `TetrisGame` is a pure state machine with no Angular in it, held in place by the
 * engine tests. Injecting a service would trade that for nothing: the component
 * can simply *observe* two snapshots, which is what `RoomPage` already does with
 * `previousMoveCount`. Pulling the decision out here — rather than inlining the
 * comparison in the component — is what makes every combination cheap to assert;
 * a real four-row clear or a real level-up takes a solver or ten cleared rows to
 * reach through the UI.
 *
 * ### One sound per step, by precedence
 *
 * `over` > `level-up` > `line-clear-quad` > `line-clear` > `move-place`. Two
 * sounds starting at the same instant are mud, so a step picks one.
 *
 * Level-up outranks a four-row clear because **it changes the game** —
 * `gravityIntervalMs` drops the moment the level does, and the player has to know.
 * A quad is a reward, and the reward is already on the scoreboard.
 *
 * `over` outranks everything for the same reason in reverse: a lock tap or a clear
 * jingle under the end-of-run sting is noise on top of the one thing that matters.
 *
 * Nothing here fires for a keypress. **Sound reports what happened, not what you
 * pressed** — a player who moved a piece sideways does not need to be told.
 */
export function soundForStep(before: TetrisProgress, after: TetrisProgress): SoundEventName | null {
  if (after.over && !before.over) return 'game-lose';
  if (after.level > before.level) return 'level-up';

  const cleared = after.lines - before.lines;
  if (cleared >= 4) return 'line-clear-quad';
  if (cleared > 0) return 'line-clear';

  return after.locks > before.locks ? 'move-place' : null;
}
