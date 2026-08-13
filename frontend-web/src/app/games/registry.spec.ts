import { describe, expect, it } from 'vitest';
import { GAME_REGISTRY } from './index';

/**
 * Invariants every manifest must hold. These run against the *real* registry
 * on purpose — they are the guard rail that keeps a newly added game honest,
 * so they must not be given a stub.
 */
describe('GAME_REGISTRY', () => {
  it('has unique keys', () => {
    const keys = GAME_REGISTRY.map((g) => g.key);
    expect(new Set(keys).size).toBe(keys.length);
  });

  it('gives every available game a non-empty launchRoute', () => {
    for (const game of GAME_REGISTRY.filter((g) => g.status === 'available')) {
      expect(game.launchRoute, `${game.key} is available and needs a launchRoute`).toBeTruthy();
    }
  });

  it('derives i18n keys from the game key', () => {
    for (const game of GAME_REGISTRY) {
      expect(game.titleKey).toBe(`games.${game.key}.title`);
      expect(game.descriptionKey).toBe(`games.${game.key}.description`);
    }
  });

  it('declares at least one content locale per game', () => {
    for (const game of GAME_REGISTRY) {
      expect(game.contentLocales.length, `${game.key} has no contentLocales`).toBeGreaterThan(0);
    }
  });

  it('uses kebab-case keys', () => {
    for (const game of GAME_REGISTRY) {
      expect(game.key).toMatch(/^[a-z0-9]+(-[a-z0-9]+)*$/);
    }
  });
});
