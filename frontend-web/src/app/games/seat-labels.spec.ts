import { describe, expect, it } from 'vitest';
import en from '../../../public/i18n/en.json';
import zhCN from '../../../public/i18n/zh-CN.json';
import { GAME_REGISTRY } from './index';
import { DefaultGameCatalogService } from './game-catalog.service';
import { seatNaming } from './seat-labels';
import { XIANGQI_ENDGAME_KEY, XIANGQI_KEY } from './xiangqi/game-key';

/** 真的 locale 文件 —— 一个手写的桩会让「键有没有译文」这条恒真。 */
const BAGS: Record<string, Record<string, unknown>> = {
  en: en as Record<string, unknown>,
  'zh-CN': zhCN as Record<string, unknown>,
};

function locale(tag: string): Record<string, unknown> {
  return BAGS[tag];
}

function lookup(bag: Record<string, unknown>, dotted: string): unknown {
  return dotted.split('.').reduce<unknown>(
    (node, part) => (node as Record<string, unknown> | undefined)?.[part],
    bag,
  );
}

const LOCALES = ['zh-CN', 'en'] as const;

describe('seat labels', () => {
  const declaring = GAME_REGISTRY.filter((g) => g.seatLabelKeys !== undefined);
  const silent = GAME_REGISTRY.filter((g) => g.seatLabelKeys === undefined);

  /**
   * **两支都要有人**,否则下面每一条都可能在一个空集合上恒真 —— 「每个声明的键都有译文」
   * 在零个声明时是绿的,「不声明的说座位号」在零个不声明时也是绿的。
   */
  it('has games on both sides of the declaration', () => {
    expect(declaring.map((g) => g.key).sort()).toEqual(
      ['gomoku', 'idiom-chain', 'tictactoe', 'xiangqi'],
      );
    expect(silent.length).toBeGreaterThan(0);
  });

  /**
   * **每一个声明的键在两份 locale 里都要有真文字。**
   *
   * 这是这一组里唯一守得住「界面上出现的是字而不是键名」的断言:漏一个译文时,
   * Transloco 渲染的是 `game.seat.red` 本身 —— 不抛、不报,只是难看,而单元测试里
   * 用手写小词典的那些断言照样绿。
   */
  it.each(LOCALES)('translates every declared seat label in %s', (tag) => {
    const bag = locale(tag);
    const missing = declaring
      .flatMap((g) => g.seatLabelKeys ?? [])
      .filter((key) => typeof lookup(bag, key) !== 'string' || lookup(bag, key) === '');

    expect(missing).toEqual([]);
  });

  /** 组成回合指示的那个句式也要有译文 —— 它是拼出来的另一半。 */
  it.each(LOCALES)('translates the composed turn phrase in %s', (tag) => {
    expect(lookup(locale(tag), 'game.turn.side-turn')).toContain('{{side}}');
    expect(lookup(locale(tag), 'game.turn.seat-turn')).toContain('{{seat}}');
    expect(lookup(locale(tag), 'game.room.seat-label')).toContain('{{seat}}');
  });

  /**
   * **退役的键不许还在**,而且不许被重新用上。
   *
   * `web-lobby` 那条规格写过这件事:重用一个退役的键名会让规格与界面各说各话。
   */
  it.each(LOCALES)('has retired the two-colour keys in %s', (tag) => {
    const bag = locale(tag);
    for (const dead of [
      'game.room.seat-black',
      'game.room.seat-white',
      'game.turn.black-turn',
      'game.turn.white-turn',
    ]) {
      expect(lookup(bag, dead)).toBeUndefined();
    }
  });

  /** 象棋族的 0 号席位是**红方** —— 这条是整个变更的判据。 */
  it('names seat 0 red in the xiangqi family, and never black', () => {
    const catalog = new DefaultGameCatalogService();
    for (const key of [XIANGQI_KEY, XIANGQI_ENDGAME_KEY]) {
      const manifest = catalog.byRoomKey(key);
      expect(seatNaming(manifest, 0, 2)).toEqual({ kind: 'named', key: 'game.seat.red' });
      expect(seatNaming(manifest, 1, 2)).toEqual({ kind: 'named', key: 'game.seat.black' });
    }
  });

  /** 反面对照:五子棋仍然是黑白 —— 否则上一条在「处处红黑」上也是绿的。 */
  it('leaves gomoku black and white', () => {
    const catalog = new DefaultGameCatalogService();
    expect(seatNaming(catalog.byRoomKey('gomoku'), 0, 2)).toEqual({
      kind: 'named',
      key: 'game.seat.black',
    });
  });

  /** 没声明的棋种说座位号。 */
  it('numbers the seats of a game that declares none', () => {
    const catalog = new DefaultGameCatalogService();
    expect(seatNaming(catalog.byRoomKey('doudizhu'), 2, 3)).toEqual({ kind: 'numbered', seat: 2 });
  });

  /**
   * **条数对不上就整间房说编号** —— 不许出现「黑方 / 白方 / 第 3 位」那种半边有名字的行。
   *
   * 这条是量出来才加的:第一版逐格判断,而一个声明两个名字、却有三个座位的棋种
   * 渲染出来就是那样,**读起来像是第三个人不算玩家**。
   */
  it('falls back to numbers for every seat when the count does not match', () => {
    const catalog = new DefaultGameCatalogService();
    const gomoku = catalog.byRoomKey('gomoku');
    for (const seat of [0, 1, 2]) {
      expect(seatNaming(gomoku, seat, 3)).toEqual({ kind: 'numbered', seat });
    }
  });

  /**
   * 伴生键要解析到主棋种,而 `byKey` **不该**解析得出来 —— 两个方向都断言,
   * 因为把它们合成一个的实现在单向断言下是绿的。
   */
  it('resolves a companion key only through byRoomKey', () => {
    const catalog = new DefaultGameCatalogService();
    expect(catalog.byKey(XIANGQI_ENDGAME_KEY)).toBeUndefined();
    expect(catalog.byRoomKey(XIANGQI_ENDGAME_KEY)?.key).toBe(XIANGQI_KEY);
  });
});
