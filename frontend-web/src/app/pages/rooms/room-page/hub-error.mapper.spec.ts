import { describe, expect, it } from 'vitest';
import { hubErrorToKey } from './hub-error.mapper';

describe('hubErrorToKey', () => {
  it('maps "not your turn" to not-your-turn', () => {
    expect(hubErrorToKey(new Error('Not your turn.'))).toBe('game.errors.not-your-turn');
  });

  it('maps "invalid move" to invalid-move', () => {
    expect(hubErrorToKey(new Error('Invalid move attempted'))).toBe('game.errors.invalid-move');
  });

  it('maps "occupied" to invalid-move', () => {
    expect(hubErrorToKey(new Error('Cell occupied'))).toBe('game.errors.invalid-move');
  });

  it('maps "out of bounds" to invalid-move', () => {
    expect(hubErrorToKey(new Error('Move out of bounds'))).toBe('game.errors.invalid-move');
  });

  it('maps "concurrent" to concurrent-move-refetched', () => {
    expect(hubErrorToKey(new Error('Concurrent update detected'))).toBe(
      'game.errors.concurrent-move-refetched',
    );
  });

  it('maps DbUpdateConcurrencyException to concurrent-move-refetched', () => {
    expect(hubErrorToKey(new Error('DbUpdateConcurrencyException thrown'))).toBe(
      'game.errors.concurrent-move-refetched',
    );
  });

  it('maps "too frequent" to urge-cooldown', () => {
    expect(hubErrorToKey(new Error('Urge too frequent'))).toBe('game.errors.urge-cooldown');
  });

  it('maps "no connection" to network', () => {
    expect(hubErrorToKey(new Error("No connection with id 'abc' was found"))).toBe(
      'game.errors.network',
    );
  });

  it('unknown message falls back to generic', () => {
    expect(hubErrorToKey(new Error('something weird'))).toBe('game.errors.generic');
  });

  it('null error maps to generic', () => {
    expect(hubErrorToKey(null)).toBe('game.errors.generic');
  });

  it('string error is matched on its content', () => {
    expect(hubErrorToKey('not your turn')).toBe('game.errors.not-your-turn');
  });

  /**
   * Xiangqi's refusals, verbatim from `XiangqiRules.Apply`.
   *
   * These matter more than the gomoku ones they sit beside. Gomoku's board only
   * lets you click an empty cell, so `invalid-move` was near-unreachable and an
   * unmapped message cost nothing. Xiangqi's board knows no rules on purpose, so a
   * refused move is how a player learns what a piece can do — and it was landing on
   * "Something went wrong. Please try again.", which reads as a broken app. Found
   * in the browser, not by reading the mapper.
   */
  describe('xiangqi refusals', () => {
    it.each([
      'A General cannot move from (9, 4) to (7, 4).',
      'There is no piece at (5, 5).',
      'The piece at (0, 0) does not belong to Black.',
      "A move must change the piece's square.",
      "'xiangqi' moves pieces; a move must carry an origin square.",
      "Position is outside the 10x9 board of 'xiangqi'.",
      '(9, 1) is occupied by your own piece.',
    ])('maps %s to invalid-move', (message) => {
      expect(hubErrorToKey(new Error(message))).toBe('game.errors.invalid-move');
    });

    it('gives self-check its own message', () => {
      // "That move is not legal" would not tell the player what they missed, and
      // hanging your own general is the most common way to be refused.
      expect(
        hubErrorToKey(
          new Error(
            'That move would leave your general in check (self-check or flying generals).',
          ),
        ),
      ).toBe('game.errors.self-check');
    });
  });
});
