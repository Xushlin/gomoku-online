import type { GameManifest } from '../game-manifest';
import { XIANGQI_ENDGAME_KEY } from './game-key';

/**
 * 中国象棋 — the platform's first *slide* game: a move is `from → to`, not a
 * placement.
 *
 * Human-vs-human and rated since `enable-xiangqi-human-play`. `launchRoute` points
 * at the **lobby**, not the AI page, exactly as gomoku's does — `gameEntryRoute`
 * reads this field, so leaving a xiangqi room lands where you would start another
 * game. `/g/xiangqi` stays as the AI entry; the lobby's own "play vs AI" card is a
 * second one, which is the pre-existing two-entrances wart `leaderboard-page`
 * already records.
 *
 * This doc comment used to explain why the game shipped AI-only: the server said
 * `SupportsHumanVsHuman: false`, so a human entry would point at an operation it
 * refused, and an unrated game's ladder would be permanently empty. Both halves are
 * now false, and neither needed a client change to become false — the descriptor
 * flipped and the catalogue followed.
 *
 * It is also the game that proved the front end had the same placement-shaped
 * assumption the Domain shed in `generalize-match-domain`: gomoku and 一字棋 are the
 * same family, so the shared `Board` had never been asked by a different one.
 */
export const xiangqiManifest: GameManifest = {
  key: 'xiangqi',
  category: 'match',
  status: 'available',
  titleKey: 'games.xiangqi.title',
  descriptionKey: 'games.xiangqi.description',
  // 一枚圆棋子，刻着「帅」。
  emblem: [
    { k: 'c', a: 12, b: 12, c: 9 },
    { k: 'c', a: 12, b: 12, c: 7 },
    { k: 't', a: 12, b: 12, c: 7.5, s: '帥' },
  ],
  // 席位名 —— 象棋是红黑,0 号座位画的是 帥。
  seatLabelKeys: ['game.seat.red', 'game.seat.black'],
  contentLocales: ['zh-CN', 'en'],
  launchRoute: '/g/xiangqi/lobby',
  // 《梅花谱》—— 只读资料,匿名可读。见 games/xiangqi/manual/。
  manualRoute: '/g/xiangqi/manual',
  manualLabelKey: 'manual.entry',
  // 残局房在服务端是另一个键,但它属于这个大厅 —— 否则「摆此局对弈」开出来的房间
  // 谁都找不到,而房主会一直等下去。
  companionRoomKeys: [XIANGQI_ENDGAME_KEY],
};
