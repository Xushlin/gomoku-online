import type { GameManifest } from '../game-manifest';

/**
 * 俄罗斯方块 — the only score-attack game, and the only one whose client owns the
 * whole rule set.
 *
 * That is the opposite of what 中国象棋 decided, and both follow the same test
 * (`add-web-klotski`): not *should the client know the rules* but *would knowing
 * them produce a second truth that can diverge*. A 60 fps falling block cannot
 * round-trip to the server, so the client has no choice — and the server replays
 * every placement, so a drifting client is refused rather than believed.
 *
 * `contentLocales: ['zh-CN', 'en']` — there is no language-bound content here at
 * all, just blocks and numbers.
 */
export const tetrisManifest: GameManifest = {
  key: 'tetris',
  category: 'score',
  status: 'available',
  titleKey: 'games.tetris.title',
  descriptionKey: 'games.tetris.description',
  icon: '块',
  launchRoute: '/g/tetris',
  contentLocales: ['zh-CN', 'en'],
};
