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
});
