import { describe, expect, it } from 'vitest';
import {
  applyMove,
  canSlide,
  initialPositions,
  isSolved,
  legalTargets,
  pieceAt,
  roleOf,
  type KlotskiLayout,
} from './model';

/** 横刀立马 minus the two middle soldiers — level 0's shape, four empty cells. */
const LAYOUT: KlotskiLayout = {
  rows: 5,
  cols: 4,
  exit: { row: 3, col: 1 },
  pieces: [
    { id: 'cao', name: '曹操', row: 0, col: 1, height: 2, width: 2, target: true },
    { id: 'zhang', name: '张飞', row: 0, col: 0, height: 2, width: 1 },
    { id: 'ma', name: '马超', row: 0, col: 3, height: 2, width: 1 },
    { id: 'zhao', name: '赵云', row: 2, col: 0, height: 2, width: 1 },
    { id: 'huang', name: '黄忠', row: 2, col: 3, height: 2, width: 1 },
    { id: 'guan', name: '关羽', row: 2, col: 1, height: 1, width: 2 },
  ],
};

describe('klotski model', () => {
  it('lays the pieces out where the level says', () => {
    const positions = initialPositions(LAYOUT);

    expect(positions['cao']).toEqual({ row: 0, col: 1 });
    expect(Object.keys(positions)).toHaveLength(6);
  });

  it('knows which piece covers a cell', () => {
    const positions = initialPositions(LAYOUT);

    expect(pieceAt(LAYOUT, positions, 1, 2)).toBe('cao');
    expect(pieceAt(LAYOUT, positions, 4, 0)).toBeNull();
  });

  it('allows a slide into empty space', () => {
    const positions = initialPositions(LAYOUT);

    // 赵云 occupies rows 2-3 col 0; row 4 col 0 is empty.
    expect(canSlide(LAYOUT, positions, 'zhao', 1, 0)).toBe(true);
  });

  it('refuses a slide into another piece', () => {
    const positions = initialPositions(LAYOUT);

    // 曹操 is boxed in by 关羽 below and the generals either side.
    expect(canSlide(LAYOUT, positions, 'cao', 1, 0)).toBe(false);
    expect(canSlide(LAYOUT, positions, 'cao', 0, -1)).toBe(false);
  });

  it('refuses a slide off the board', () => {
    const positions = initialPositions(LAYOUT);

    expect(canSlide(LAYOUT, positions, 'cao', -1, 0)).toBe(false);
    expect(canSlide(LAYOUT, positions, 'zhang', -1, 0)).toBe(false);
  });

  it('refuses a slide for a piece that does not exist', () => {
    expect(canSlide(LAYOUT, initialPositions(LAYOUT), 'nobody', 1, 0)).toBe(false);
  });

  it('offers only the legal destinations', () => {
    const positions = initialPositions(LAYOUT);

    // 关羽 spans (2,1)-(2,2); below it row 3 cols 1-2 is empty, above is 曹操.
    const targets = legalTargets(LAYOUT, positions, 'guan');

    expect(targets).toEqual([{ row: 3, col: 1, dr: 1, dc: 0 }]);
  });

  it('offers nothing for a piece hemmed in on all sides', () => {
    expect(legalTargets(LAYOUT, initialPositions(LAYOUT), 'cao')).toEqual([]);
  });

  it('moves a piece without mutating the previous state', () => {
    const before = initialPositions(LAYOUT);
    const after = applyMove(before, { id: 'guan', dr: 1, dc: 0 });

    expect(after['guan']).toEqual({ row: 3, col: 1 });
    expect(before['guan']).toEqual({ row: 2, col: 1 });
  });

  it('frees the vacated cells after a move', () => {
    const after = applyMove(initialPositions(LAYOUT), { id: 'guan', dr: 1, dc: 0 });

    expect(pieceAt(LAYOUT, after, 2, 1)).toBeNull();
    expect(canSlide(LAYOUT, after, 'cao', 1, 0)).toBe(true);
  });

  it('is solved only when the target reaches the exit', () => {
    let positions = initialPositions(LAYOUT);
    expect(isSolved(LAYOUT, positions)).toBe(false);

    positions = applyMove(positions, { id: 'guan', dr: 1, dc: 0 });
    positions = applyMove(positions, { id: 'cao', dr: 1, dc: 0 });
    expect(isSolved(LAYOUT, positions)).toBe(false);

    positions = applyMove(positions, { id: 'cao', dr: 1, dc: 0 });
    expect(positions['cao']).toEqual({ row: 2, col: 1 });
    expect(isSolved(LAYOUT, positions)).toBe(false);

    positions = applyMove(positions, { id: 'cao', dr: 1, dc: 0 });
    expect(isSolved(LAYOUT, positions)).toBe(true);
  });

  it('is not solved by a layout with no target piece', () => {
    const targetless: KlotskiLayout = {
      ...LAYOUT,
      pieces: LAYOUT.pieces.map((p) => ({ ...p, target: false })),
    };

    expect(isSolved(targetless, initialPositions(targetless))).toBe(false);
  });
});

/*
 * 角色是**推出来**的,而这组用例是它的合同。四类都在样本里 —— 一个只覆盖两类的样本
 * 会让「四类两两不同」那条渲染断言恒真。
 */
describe('roleOf', () => {
  it('classifies the four shapes a real level uses', () => {
    expect(roleOf({ width: 2, height: 2, target: true })).toBe('boss');
    expect(roleOf({ width: 1, height: 2, target: undefined })).toBe('general');
    expect(roleOf({ width: 2, height: 1, target: undefined })).toBe('guard');
    expect(roleOf({ width: 1, height: 1, target: undefined })).toBe('soldier');
  });

  it('is total — every shape lands somewhere, so no undefined reaches the template', () => {
    const shapes = [
      { width: 1, height: 3 },
      { width: 3, height: 1 },
      { width: 2, height: 2 },
      { width: 3, height: 3 },
      { width: 3, height: 2 },
    ];
    for (const shape of shapes) {
      expect(['boss', 'general', 'guard', 'soldier']).toContain(roleOf({ ...shape, target: undefined }));
    }
  });

  it('does not look at the name — a level may call its pieces anything', () => {
    const shape = { width: 1, height: 2, target: undefined };
    expect(roleOf(shape)).toBe(roleOf({ ...shape }));
    // 同一个形状换个「名字」不改变分类:分类的入参里根本没有名字这一项,
    // 而这条断言存在的理由是那句话哪天不再成立时它会红。
    const withName = { ...shape, name: '一个完全不同的名字' } as never;
    expect(roleOf(withName)).toBe('general');
  });

  it('sends the target piece to boss whatever its size', () => {
    expect(roleOf({ width: 1, height: 1, target: true })).toBe('boss');
  });
});
