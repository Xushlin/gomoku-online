/** The game key, shared by the manifest, the routes and every API call. */
export const KLOTSKI_KEY = 'klotski';

/** One piece as the level artefact describes it. `name` is display content, not UI copy. */
export interface KlotskiPiece {
  readonly id: string;
  readonly name?: string;
  readonly row: number;
  readonly col: number;
  readonly height: number;
  readonly width: number;
  readonly target?: boolean;
}

/** 一个棋子在盘上扮演的角色 —— 决定它长什么样,不决定它怎么走。 */
export type KlotskiRole = 'boss' | 'general' | 'guard' | 'soldier';

/**
 * 角色**从几何形状推出来**,不看名字、不看 id、不查名单。
 *
 * 理由是量出来的:改这条之前六个棋子里**五个是同一个颜色**,唯一的区分是那两个汉字。
 * 而 `name` 是关卡数据里的自由文本 —— 任何按名字分类的写法都会在下一份 `layoutJson`
 * 上悄悄退化成「全都是默认那一类」,而退化的样子和正常的样子一模一样。尺寸是规则本身。
 *
 * 分类是**全的**:每个 `width x height` 组合都落进四类之一,所以模板里不会有
 * `undefined` 流出来,也不需要第五个只在假想关卡里出现的兜底皮肤 token
 * (一个没有调用点的 token 是每个皮肤都要付的死条目 —— `--radius-pill` 那条记过)。
 */
export function roleOf(piece: Pick<KlotskiPiece, 'width' | 'height' | 'target'>): KlotskiRole {
  if (piece.target === true) return 'boss';
  if (piece.width === 1 && piece.height === 1) return 'soldier';
  return piece.height > piece.width ? 'general' : 'guard';
}

export interface KlotskiExit {
  readonly row: number;
  readonly col: number;
}

/** A level's opaque `layoutJson`, parsed. */
export interface KlotskiLayout {
  readonly rows: number;
  readonly cols: number;
  readonly name?: string;
  readonly exit: KlotskiExit;
  readonly pieces: readonly KlotskiPiece[];
}

/** Where each piece currently is. Sizes never change, so only the corner is tracked. */
export type KlotskiPositions = Readonly<Record<string, { readonly row: number; readonly col: number }>>;

/** One slide, in the shape the server replays. */
export interface KlotskiMove {
  readonly id: string;
  readonly dr: number;
  readonly dc: number;
}

export const DIRECTIONS: readonly { readonly dr: number; readonly dc: number }[] = [
  { dr: -1, dc: 0 },
  { dr: 1, dc: 0 },
  { dr: 0, dc: -1 },
  { dr: 0, dc: 1 },
];

export function initialPositions(layout: KlotskiLayout): KlotskiPositions {
  const out: Record<string, { row: number; col: number }> = {};
  for (const piece of layout.pieces) out[piece.id] = { row: piece.row, col: piece.col };
  return out;
}

/**
 * Can this piece slide one cell in this direction?
 *
 * **This is the whole rule of 华容道**, and it is why this client judges its own
 * moves while the xiangqi board judges none of its own.
 *
 * 象棋's board deliberately knows no rules: a TypeScript port of them would be a
 * second source of truth that could silently disagree with the server, and nothing
 * would notice. Here there is no second source of truth to create — a client that
 * could not answer this question could not draw a legal drag, let alone animate one.
 * The rule is one line, and it is the same line on both sides.
 */
export function canSlide(
  layout: KlotskiLayout,
  positions: KlotskiPositions,
  id: string,
  dr: number,
  dc: number,
): boolean {
  const piece = layout.pieces.find((p) => p.id === id);
  const at = positions[id];
  if (!piece || !at) return false;

  const row = at.row + dr;
  const col = at.col + dc;
  if (row < 0 || col < 0 || row + piece.height > layout.rows || col + piece.width > layout.cols) {
    return false;
  }

  for (let r = row; r < row + piece.height; r++) {
    for (let c = col; c < col + piece.width; c++) {
      const occupant = pieceAt(layout, positions, r, c);
      if (occupant !== null && occupant !== id) return false;
    }
  }
  return true;
}

/** Which piece covers this cell, if any. */
export function pieceAt(
  layout: KlotskiLayout,
  positions: KlotskiPositions,
  row: number,
  col: number,
): string | null {
  for (const piece of layout.pieces) {
    const at = positions[piece.id];
    if (!at) continue;
    if (
      row >= at.row &&
      row < at.row + piece.height &&
      col >= at.col &&
      col < at.col + piece.width
    ) {
      return piece.id;
    }
  }
  return null;
}

/** The legal one-cell destinations for a piece, as top-left corners. */
export function legalTargets(
  layout: KlotskiLayout,
  positions: KlotskiPositions,
  id: string,
): readonly { readonly row: number; readonly col: number; readonly dr: number; readonly dc: number }[] {
  const at = positions[id];
  if (!at) return [];
  return DIRECTIONS.filter((d) => canSlide(layout, positions, id, d.dr, d.dc)).map((d) => ({
    row: at.row + d.dr,
    col: at.col + d.dc,
    dr: d.dr,
    dc: d.dc,
  }));
}

/** Apply a slide. Returns a new positions map; the caller checks legality first. */
export function applyMove(positions: KlotskiPositions, move: KlotskiMove): KlotskiPositions {
  const at = positions[move.id];
  if (!at) return positions;
  return { ...positions, [move.id]: { row: at.row + move.dr, col: at.col + move.dc } };
}

/** Is the target piece on the exit? */
export function isSolved(layout: KlotskiLayout, positions: KlotskiPositions): boolean {
  const target = layout.pieces.find((p) => p.target);
  if (!target) return false;
  const at = positions[target.id];
  return !!at && at.row === layout.exit.row && at.col === layout.exit.col;
}
