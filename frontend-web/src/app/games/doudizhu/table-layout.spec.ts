import { describe, expect, it } from 'vitest';

import { relativeSeat, seatInitial, seatRing } from './table-layout';

describe('seatRing', () => {
  it('starts at my seat and wraps', () => {
    expect(seatRing(0, 3)).toEqual([0, 1, 2]);
    expect(seatRing(1, 3)).toEqual([1, 2, 0]);
    expect(seatRing(2, 3)).toEqual([2, 0, 1]);
  });

  it('seats a spectator in seat 0 chair', () => {
    // 围观者总得坐在某个方向上,而「座位表里的第一个」是唯一不需要额外约定就说得清的选择。
    expect(seatRing(null, 3)).toEqual([0, 1, 2]);
  });

  it('is empty rather than throwing when there are no seats', () => {
    expect(seatRing(null, 0)).toEqual([]);
  });
});

describe('relativeSeat', () => {
  it('puts the next player on my right', () => {
    // **下家在右手边** —— 出牌逆时针,俯视时从下方逆时针数的下一个位置就是右边。
    expect(relativeSeat(1, 1, 3)).toBe('self');
    expect(relativeSeat(2, 1, 3)).toBe('right');
    expect(relativeSeat(0, 1, 3)).toBe('left');
  });

  it('is the same shape from every chair', () => {
    for (const me of [0, 1, 2]) {
      const directions = [0, 1, 2].map((seat) => relativeSeat(seat, me, 3));
      // 每个方向恰好一次 —— 两个座位落在同一个格子上就是画错。
      expect([...directions].sort()).toEqual(['left', 'right', 'self']);
    }
  });

  it('gives a fourth player their own direction', () => {
    // 挖坑是四个人。没有 `across` 的话第四个人会和另一个人叠在同一个位置上 —— 画错,不是少画。
    expect([0, 1, 2, 3].map((seat) => relativeSeat(seat, 0, 4))).toEqual([
      'self',
      'right',
      'across',
      'left',
    ]);
  });

  it('does not throw on a seat number that is not in the ring', () => {
    expect(relativeSeat(9, 0, 3)).toBe('self');
    expect(relativeSeat(0, 0, 0)).toBe('self');
  });
});

describe('seatInitial', () => {
  it('takes the first code point, not the first UTF-16 unit', () => {
    // `name[0]` 会把一个增补平面字符切成半个代理对,屏幕上是一个替换字符。
    expect(seatInitial('alice')).toBe('A');
    expect(seatInitial('张三')).toBe('张');
    expect(seatInitial('𝒜lice')).toBe('𝒜');
  });

  it('falls back to a question mark rather than an empty circle', () => {
    expect(seatInitial('')).toBe('?');
  });
});
