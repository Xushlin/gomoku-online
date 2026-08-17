import { describe, expect, it } from 'vitest';
import { DefaultGameCatalogService } from './game-catalog.service';
import { gameEntryRoute, PLATFORM_HOME } from './game-entry-route';
import { GAME_REGISTRY } from './index';

const catalog = new DefaultGameCatalogService();

describe('gameEntryRoute', () => {
  it('sends a gomoku player to gomoku\'s lobby', () => {
    expect(gameEntryRoute(catalog, 'gomoku')).toBe('/g/gomoku/lobby');
  });

  it('sends a game without a lobby to its own entry page', () => {
    // 象棋 and 一字棋 have no human-vs-human mode, so no room list — but their
    // AI page is where you start another one, which is what "back" means here.
    expect(gameEntryRoute(catalog, 'xiangqi')).toBe('/g/xiangqi');
    expect(gameEntryRoute(catalog, 'tictactoe')).toBe('/g/tictactoe');
  });

  it('falls back to the platform home for a key this client does not know', () => {
    // A server newer than the client will name games this build has never heard
    // of. Guessing `/g/<key>/lobby` would route to a page that renders "no such
    // game" — worse than admitting we do not know where to go.
    expect(gameEntryRoute(catalog, 'go')).toBe(PLATFORM_HOME);
  });

  it('falls back when there is no key at all', () => {
    // The room never loaded. Nothing to resolve.
    expect(gameEntryRoute(catalog, null)).toBe(PLATFORM_HOME);
    expect(gameEntryRoute(catalog, undefined)).toBe(PLATFORM_HOME);
    expect(gameEntryRoute(catalog, '')).toBe(PLATFORM_HOME);
  });

  it('resolves every playable game to somewhere that is not the platform home', () => {
    // Walks the registry rather than listing games, so a new one is covered by
    // existing. The assertion that matters is the negative: a manifest still
    // pointing at `/home` would silently make this helper a no-op for that game.
    const playable = GAME_REGISTRY.filter((g) => g.status === 'available');

    expect(playable.length).toBeGreaterThan(0);
    for (const game of playable) {
      expect(gameEntryRoute(catalog, game.key), `${game.key}`).not.toBe(PLATFORM_HOME);
    }
  });
});
