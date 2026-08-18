import { describe, expect, it } from 'vitest';
import { soundForStep, type TetrisProgress } from './announce';

const START: TetrisProgress = { locks: 0, lines: 0, level: 1, over: false };

function after(patch: Partial<TetrisProgress>): TetrisProgress {
  return { ...START, ...patch };
}

/**
 * The precedence table, asserted directly.
 *
 * A real four-row clear or a real level-up costs a solver or ten cleared rows to
 * reach through the component — which is why the decision is a pure function
 * taking two snapshots. Here the numbers can just be stated.
 */
describe('soundForStep', () => {
  it('says nothing when nothing happened', () => {
    expect(soundForStep(START, START)).toBeNull();
  });

  it('a lock with no clear is a move', () => {
    expect(soundForStep(START, after({ locks: 1 }))).toBe('move-place');
  });

  it.each([1, 2, 3])('clearing %i row(s) plays line-clear, not the lock tap', (lines) => {
    expect(soundForStep(START, after({ locks: 1, lines }))).toBe('line-clear');
  });

  it('four rows at once is its own sound', () => {
    // The 100-vs-800 gap is the whole "save up for a tetris" decision; a sound
    // that does not mark it contradicts the scoreboard.
    expect(soundForStep(START, after({ locks: 1, lines: 4 }))).toBe('line-clear-quad');
  });

  it('a level-up outranks the clear that caused it', () => {
    // Level-up changes the game — gravity speeds up the instant it happens — while
    // a quad is a reward already visible on the scoreboard.
    const before = after({ locks: 12, lines: 8, level: 1 });
    const now = after({ locks: 13, lines: 12, level: 2 });

    expect(soundForStep(before, now)).toBe('level-up');
  });

  it('the end of the run outranks everything', () => {
    const before = after({ locks: 40, lines: 18, level: 2 });
    const now = { locks: 41, lines: 22, level: 3, over: true };

    expect(soundForStep(before, now)).toBe('game-lose');
  });

  it('announces the end exactly once', () => {
    const over = after({ locks: 5, over: true });

    expect(soundForStep(after({ locks: 4 }), over)).toBe('game-lose');
    expect(soundForStep(over, over)).toBeNull();
  });

  it('a step that only moved the piece sideways is silent', () => {
    // Nothing in the snapshot changes on a lateral move or a rotation, and that is
    // deliberate: sound reports what happened, not what was pressed.
    expect(soundForStep(after({ locks: 3, lines: 2 }), after({ locks: 3, lines: 2 }))).toBeNull();
  });
});
