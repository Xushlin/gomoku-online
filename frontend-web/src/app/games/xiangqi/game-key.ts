/**
 * The game key, shared by the manifest, the route, the board switch, and every
 * API call.
 *
 * It must match `GameKeys.Xiangqi` on the server — that string is what the rules
 * and AI registries resolve on, so a typo here produces a rejected request rather
 * than a compile error.
 */
export const XIANGQI_KEY = 'xiangqi';

/**
 * 从一则古谱残局开局的那个棋种 —— 服务端的 `GameKeys.XiangqiEndgame`。
 *
 * **它是一个独立的键,而那是内核不变量逼出来的**:`Room` 用类型判断「这局要不要一份
 * 开局设置」,而且两个方向都抛。象棋「有时要、有时不要」会同时删掉那两个方向的检查。
 * 一个独立的键让老键**从不**要设置、新键**总是**要,两个检查都还在。
 *
 * 服务端的理由写在 `IPositionalStartRules` 与 `XiangqiEndgameRules` 的类注释里。
 */
export const XIANGQI_ENDGAME_KEY = 'xiangqi-endgame';

/**
 * 这个键是不是「象棋族」—— 同一块 10×9 的棋盘、同一套走子规则,只是开局的局面不同。
 *
 * 画棋盘的地方 MUST 用它,而不是 `key === XIANGQI_KEY`:后者在残局房里会静静落到
 * 默认的方格棋盘,而**一个画错的棋盘不会抛任何东西**,它只是画错。
 */
export function isXiangqiFamily(gameKey: string | null | undefined): boolean {
  return gameKey === XIANGQI_KEY || gameKey === XIANGQI_ENDGAME_KEY;
}
