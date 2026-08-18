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

  it('has 成语纵横 available at its own route', () => {
    const crossword = GAME_REGISTRY.find((g) => g.key === 'idiom-crossword');

    expect(crossword?.status).toBe('available');
    expect(crossword?.category).toBe('puzzle');
    expect(crossword?.launchRoute).toBe('/g/idiom-crossword');
    // Chinese-content game: the UI translates, the 成语 do not.
    expect(crossword?.contentLocales).toEqual(['zh-CN']);
  });

  it('has 中国象棋 available, entering through its lobby', () => {
    const xiangqi = GAME_REGISTRY.find((g) => g.key === 'xiangqi');

    expect(xiangqi?.status).toBe('available');
    expect(xiangqi?.category).toBe('match');
    // The lobby, not `/g/xiangqi`. Human-vs-human since `enable-xiangqi-human-play`,
    // so the entry is a room list like gomoku's; the AI page stays reachable from
    // the lobby's own card.
    expect(xiangqi?.launchRoute).toBe('/g/xiangqi/lobby');
    // No board dimensions here: 象棋's are 10×9, they come from GET /api/games,
    // and its board component hardcodes them anyway because an intersection
    // board is not a parameterisation of a grid of cells. A manifest copy would
    // have been read by nobody — see board-size.spec.ts.
  });

  it('has 俄罗斯方块 available at its own route', () => {
    const tetris = GAME_REGISTRY.find((g) => g.key === 'tetris');

    expect(tetris?.status).toBe('available');
    // The only score-attack game — and the only sample keeping the catalogue's
    // "score games get a high-scores link" branch from being a no-op.
    expect(tetris?.category).toBe('score');
    expect(tetris?.launchRoute).toBe('/g/tetris');
    // Nothing here is language-bound: blocks and numbers.
    expect(tetris?.contentLocales).toEqual(['zh-CN', 'en']);
  });

  it('still has exactly one score-attack game', () => {
    // `ScoreAttackGames` on the server is a one-armed switch on purpose. If a second
    // score game appears, that switch becomes a registry — and this test is where
    // the reminder lands.
    expect(GAME_REGISTRY.filter((g) => g.category === 'score')).toHaveLength(1);
  });

  it('uses kebab-case keys', () => {
    for (const game of GAME_REGISTRY) {
      expect(game.key).toMatch(/^[a-z0-9]+(-[a-z0-9]+)*$/);
    }
  });
});
