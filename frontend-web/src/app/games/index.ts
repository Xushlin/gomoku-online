import type { GameManifest } from './game-manifest';
import { doudizhuManifest } from './doudizhu/manifest';
import { gomokuManifest } from './gomoku/manifest';
import { idiomChainManifest } from './idiom-chain/manifest';
import { idiomCrosswordManifest } from './idiom-crossword/manifest';
import { idiomGuessManifest } from './idiom-guess/manifest';
import { klotskiManifest } from './klotski/manifest';
import { tetrisManifest } from './tetris/manifest';
import { ticTacToeManifest } from './tictactoe/manifest';
import { wakengManifest } from './wakeng/manifest';
import { xiangqiManifest } from './xiangqi/manifest';

/**
 * The platform's game registry — the single place a new game is registered.
 *
 * Available games first, then planned ones in roadmap order. Everything the
 * catalogue shows is derived from this array, so shipping a game is a one-line
 * edit here plus its own folder and locale keys.
 */
export const GAME_REGISTRY: readonly GameManifest[] = [
  gomokuManifest,
  idiomCrosswordManifest,
  idiomChainManifest,
  idiomGuessManifest,
  ticTacToeManifest,
  xiangqiManifest,
  doudizhuManifest,
  wakengManifest,
  klotskiManifest,
  tetrisManifest,
];
