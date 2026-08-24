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
  // 框里一个 2×2 大块、几个小块，右下留空 —— 空格是唯一能动的地方。
  emblem: [
    { k: 'r', a: 4, b: 4, c: 16, d: 16, r: 1.4 },
    { k: 'r', a: 6, b: 6, c: 8, d: 8, r: 1, f: 1 },
    { k: 'r', a: 14, b: 6, c: 4, d: 8, r: 1 },
    { k: 'r', a: 6, b: 14, c: 4, d: 4, r: 1 },
    { k: 'r', a: 10, b: 14, c: 4, d: 4, r: 1 },
  ],
  launchRoute: '/g/klotski',
  contentLocales: ['zh-CN'],
};
