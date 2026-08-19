import { describe, expect, it } from 'vitest';
import { myOutcome } from './outcome';

describe('myOutcome', () => {
  it('the winner sees a win', () => {
    expect(myOutcome({ result: 'Decided', winnerUserId: 'u-1' }, 'u-1')).toBe('win');
  });

  it('the loser sees a loss', () => {
    expect(myOutcome({ result: 'Decided', winnerUserId: 'u-1' }, 'u-2')).toBe('lose');
  });

  it('a draw is a draw for everyone', () => {
    expect(myOutcome({ result: 'Draw', winnerUserId: null }, 'u-1')).toBe('draw');
    expect(myOutcome({ result: 'Draw', winnerUserId: null }, null)).toBe('draw');
  });

  it('a spectator is not the winner', () => {
    // Not a win — but the reason matters. The old form was
    // `result === 'BlackWin' && mySide === 'black'`, and a spectator held neither
    // mirror, so it fell off the end of the branch list. Here they are simply not the
    // winner, which is a true statement rather than a gap.
    expect(myOutcome({ result: 'Decided', winnerUserId: 'u-1' }, null)).toBe('lose');
    expect(myOutcome({ result: 'Decided', winnerUserId: 'u-1' }, undefined)).toBe('lose');
  });

  it('a null winner is nobody, not everybody', () => {
    // `Decided` always names a winner (the server's constructor enforces it), so this
    // pair should never arrive. If it ever does, "I won" MUST NOT be the answer for the
    // user whose id is also missing — `null === null` would otherwise read as a win.
    expect(myOutcome({ result: 'Decided', winnerUserId: null }, null)).toBe('lose');
  });
});
