import type { GameManifest } from '../game-manifest';

/**
 * 华容道 — the sliding-block puzzle, and the puzzle kernel's proof.
 *
 * 成语纵横 is server-authoritative because it *withholds* the answer. 华容道 hides
 * nothing — the pieces, the board, the exit and the one sliding rule are all public
 * and all on the client, because a client that could not judge a slide could not
 * animate one. It is authoritative because the server *replays* the whole move list.
 *
 * `contentLocales: ['zh-CN']` — the piece faces are 曹操 / 关羽 / 张飞, which are
 * content rather than UI copy and are not translated, exactly like 成语纵横's idioms.
 */
export const klotskiManifest: GameManifest = {
  key: 'klotski',
  category: 'puzzle',
  status: 'available',
  titleKey: 'games.klotski.title',
  descriptionKey: 'games.klotski.description',
  icon: '华',
  launchRoute: '/g/klotski',
  contentLocales: ['zh-CN'],
};
