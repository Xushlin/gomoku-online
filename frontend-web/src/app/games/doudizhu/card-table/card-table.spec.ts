import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { beforeEach, describe, expect, it } from 'vitest';

import type { RoomState } from '../../../core/api/models/room.model';
import { decodeCard, decodeHand, encodeHand } from '../cards';
import { parseSeatView } from '../seat-view';
import { CardTable } from './card-table';

/** 一份 `seatView` 载荷 —— 形状与服务端 `DoudizhuSeatView` 一致。 */
function seatView(overrides: Record<string, unknown> = {}): string {
  return JSON.stringify({
    phase: 'Playing',
    landlord: 0,
    baseScore: 2,
    bidsMade: 3,
    myHand: 'ABC',
    handCounts: [3, 17, 17],
    kitty: 'DEF',
    tableSeat: null,
    tableCards: null,
    ...overrides,
  });
}

function room(view: string | null, currentSeat = 0): RoomState {
  return {
    id: 'r-1',
    name: 'ddz',
    gameKey: 'doudizhu',
    status: 'Playing',
    host: { id: 'u-1', username: 'a' },
    black: { id: 'u-1', username: 'a' },
    white: { id: 'u-2', username: 'b' },
    seats: [
      { index: 0, player: { id: 'u-1', username: 'a' } },
      { index: 1, player: { id: 'u-2', username: 'b' } },
      { index: 2, player: { id: 'u-3', username: 'c' } },
    ],
    spectators: [],
    game: {
      id: 'g-1',
      currentSeat,
      startedAt: 'x',
      endedAt: null,
      result: null,
      winnerUserId: null,
      endReason: null,
      turnStartedAt: 'x',
      turnTimeoutSeconds: 60,
      moves: [],
      seatView: view,
    },
    chatMessages: [],
    createdAt: 'x',
  };
}

@Component({
  standalone: true,
  imports: [CardTable],
  template: `<app-card-table
    [state]="state()"
    [mySeat]="mySeat()"
    (action)="actions.push($event)"
  />`,
})
class Host {
  readonly state = signal<RoomState | null>(room(seatView()));
  readonly mySeat = signal<number | null>(0);
  readonly actions: string[] = [];
}

function mount() {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      Host,
      TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
  });
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  return fixture;
}

function root(fixture: ReturnType<typeof mount>): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

function handButtons(fixture: ReturnType<typeof mount>): HTMLButtonElement[] {
  return [...root(fixture).querySelectorAll<HTMLButtonElement>('[role="group"] button')];
}

function actionButtons(fixture: ReturnType<typeof mount>): HTMLButtonElement[] {
  return [...root(fixture).querySelectorAll<HTMLButtonElement>('button')].filter(
    (b) => !b.closest('[role="group"]'),
  );
}

describe('cards', () => {
  it('decodes the alphabet the server encodes with', () => {
    // 编码是持久化格式,所以这几个值是钉死的:'A' 是最小的一张(♣3),'@' / '#' 是两张王。
    expect(decodeCard('A')).toMatchObject({ rank: 3, suit: 'clubs', label: '3', red: false });
    expect(decodeCard('C')).toMatchObject({ rank: 3, suit: 'hearts', red: true });
    expect(decodeCard('@')).toMatchObject({ rank: 16, label: '小', suit: 'none' });
    expect(decodeCard('#')).toMatchObject({ rank: 17, label: '大', suit: 'none' });
  });

  it('returns null for a character it does not know', () => {
    // 不抛:一个未来的服务端多送一张这个构建不认识的牌,该表现为那一张画不出来,而不是整页崩掉。
    expect(decodeCard('!')).toBeNull();
    expect(decodeCard('AB')).toBeNull();
    expect(decodeHand('A!B').map((c) => c.code)).toEqual(['A', 'B']);
  });

  it('encodes a selection in ascending order', () => {
    // 服务端的编码是排序过的,所以同一手牌只有一种写法。
    const cards = decodeHand('QA@');
    expect(encodeHand(cards)).toBe('AQ@');
  });
});

describe('parseSeatView', () => {
  it('is null for a missing, empty or unreadable payload', () => {
    // 三种"解不出来"都走这条路:字段不在、对局还没开始、形状读不懂。
    expect(parseSeatView(null)).toBeNull();
    expect(parseSeatView(undefined)).toBeNull();
    expect(parseSeatView('')).toBeNull();
    expect(parseSeatView('not json')).toBeNull();
    expect(parseSeatView('{"phase":"Nonsense"}')).toBeNull();
  });

  it('keeps a null kitty null rather than an empty hand', () => {
    // 「底牌还没翻开」与「底牌是空的」必须是两个不同的答案 —— 前者在叫分阶段是常态。
    expect(parseSeatView(seatView({ kitty: null }))?.kitty).toBeNull();
    expect(parseSeatView(seatView({ kitty: 'DEF' }))?.kitty).toHaveLength(3);
  });
});

describe('CardTable', () => {
  let fixture: ReturnType<typeof mount>;

  beforeEach(() => {
    fixture = mount();
  });

  it('shows my hand and only my hand', () => {
    // 裁剪是服务端做的,所以这里断言的是"画出来的就是服务端给的那几张"。
    expect(handButtons(fixture).map((b) => b.getAttribute('aria-label'))).toEqual([
      '♣3',
      '♦3',
      '♥3',
    ]);
  });

  it('emits a bid during the bidding phase', () => {
    fixture.componentInstance.state.set(room(seatView({ phase: 'Bidding', landlord: null })));
    fixture.detectChanges();

    actionButtons(fixture)[1].click();

    expect(fixture.componentInstance.actions).toEqual(['bid:1']);
  });

  it('emits the selected cards in ascending order', () => {
    const hand = handButtons(fixture);
    hand[2].click();
    hand[0].click();
    fixture.detectChanges();

    actionButtons(fixture).find((b) => !b.disabled)!.click();

    expect(fixture.componentInstance.actions).toEqual(['play:AC']);
  });

  it('cannot pass on a free lead but can once there is something to beat', () => {
    // 这一条**不需要规则**:桌上没牌就是没牌。它是"客户端判得出"那一侧的边界 ——
    // 再往前一步(这手压不压得住)就要一整套牌型识别,而那会造出第二个真源。
    const pass = () => actionButtons(fixture).at(-1)!;
    expect(pass().disabled).toBe(true);

    fixture.componentInstance.state.set(room(seatView({ tableSeat: 1, tableCards: 'Q' })));
    fixture.detectChanges();

    expect(pass().disabled).toBe(false);
    pass().click();
    expect(fixture.componentInstance.actions).toEqual(['pass']);
  });

  it('disables everything off-turn', () => {
    fixture.componentInstance.state.set(room(seatView(), 1));
    fixture.detectChanges();

    expect(handButtons(fixture).every((b) => b.disabled)).toBe(true);
    expect(actionButtons(fixture).every((b) => b.disabled)).toBe(true);
  });

  it('treats seat 2 as a player, not a spectator', () => {
    // **这条是 `mySide` 换成 `mySeat` 的理由。** `'black' | 'white' | 'spectator'` 对
    // 第三个座位无话可说,于是 2 号座位上的人会被当成围观者:牌都不给他画。
    fixture.componentInstance.mySeat.set(2);
    fixture.componentInstance.state.set(room(seatView({ handCounts: [17, 17, 3] }), 2));
    fixture.detectChanges();

    expect(handButtons(fixture)).toHaveLength(3);
    expect(handButtons(fixture).every((b) => b.disabled)).toBe(false);
  });

  it('gives a spectator no hand and no buttons', () => {
    fixture.componentInstance.mySeat.set(null);
    fixture.componentInstance.state.set(room(seatView({ myHand: '' })));
    fixture.detectChanges();

    expect(handButtons(fixture)).toHaveLength(0);
    expect(actionButtons(fixture)).toHaveLength(0);
  });

  it('offers no actions once the game is finished', () => {
    // **在浏览器里发现的。** 一局流掉之后,牌桌还在画「出牌 / 不要」两个(禁用的)按钮 ——
    // 一个点不动的按钮在屏幕上是个问句:是我不能点,还是坏了?对局结束就没有动作可做。
    fixture.componentInstance.state.set(room(seatView({ phase: 'Finished' })));
    fixture.detectChanges();

    expect(actionButtons(fixture)).toHaveLength(0);
    expect(handButtons(fixture)).toHaveLength(3);
  });

  it('renders a placeholder instead of throwing when there is no seat view', () => {
    // 等待发牌、棋种没有隐藏状态、载荷读不懂 —— 三种都不该让房间页挂掉。
    fixture.componentInstance.state.set(room(null));

    expect(() => fixture.detectChanges()).not.toThrow();
    expect(handButtons(fixture)).toHaveLength(0);
  });

  it('shows the other two seats in table order starting after mine', () => {
    fixture.componentInstance.mySeat.set(1);
    fixture.componentInstance.state.set(room(seatView({ handCounts: [5, 3, 9] }), 1));
    fixture.detectChanges();

    // 断言读 `data-seat` / `data-count` 而不是文本:测试里的 transloco 没有翻译表,
    // 所以插值过的文字全是键名 —— **一条读那段文字的断言会永远通过或永远失败,而两种都没在验东西**。
    const seats = [...root(fixture).querySelectorAll('ul li')];
    expect(seats.map((li) => li.getAttribute('data-seat'))).toEqual(['2', '0']);
    expect(seats.map((li) => li.getAttribute('data-count'))).toEqual(['9', '5']);
  });
});
