import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { beforeEach, describe, expect, it } from 'vitest';
import type { RoomState } from '../../core/api/models/room.model';
import type { CardTableConfig } from '../cards/card-table-config';
import { CardTable } from '../cards/card-table/card-table';
import { decodeCard } from '../cards/cards';
import { DOUDIZHU_TABLE } from '../doudizhu/seat-view';
import { WAKENG_TABLE } from './seat-view';
import { compareWakengForDisplay, wakengStrength } from './strength';

/**
 * 挖坑的牌桌 —— **只测与斗地主不同的那几件事**。
 *
 * 牌桌是共享的一份,所以「扇形怎么排、牌背怎么叠、选牌怎么高亮」在
 * `cards/card-table/card-table.spec.ts` 里已经钉过,这里不重复。这一个文件回答的是:
 * **配置真的被读了吗?** 每一条断言都构造成:**如果配置换成斗地主那份,它会红。**
 */

/** ♣4(编码里最小的一张)与 ♣3 —— 编码序里 3 在前,而挖坑里 3 最大。 */
const C3 = 'A'; // index 0 → rank 3, clubs
const C4 = 'E'; // index 4 → rank 4, clubs

function seatView(over: Record<string, unknown> = {}): string {
  return JSON.stringify({
    phase: 'Bidding',
    firstBidder: 2,
    firstBidderCard: C4,
    digger: null,
    bid: 0,
    bidsMade: 0,
    // **手牌刻意含 3 与 4** —— 否则编码序与挖坑的大小给出同一个结果,
    // 而那条排序断言会因为别的理由通过。
    myHand: C3 + C4,
    handCounts: [16, 16, 16],
    kitty: null,
    tableSeat: null,
    tableCards: null,
    winner: null,
    ...over,
  });
}

function room(view: string): RoomState {
  return {
    id: 'r-1',
    name: 'wakeng',
    gameKey: 'wakeng',
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
      currentSeat: 2,
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
  } as unknown as RoomState;
}

@Component({
  standalone: true,
  imports: [CardTable],
  template: `<app-card-table [state]="state()" [config]="config()" [mySeat]="mySeat()" />`,
})
class Host {
  readonly state = signal<RoomState | null>(room(seatView()));
  readonly config = signal<CardTableConfig>(WAKENG_TABLE);
  readonly mySeat = signal<number | null>(2);
}

function mount(config: CardTableConfig = WAKENG_TABLE) {
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
  fixture.componentInstance.config.set(config);
  fixture.detectChanges();
  return fixture;
}

const cardLabels = (fixture: ReturnType<typeof mount>): string[] =>
  [...fixture.nativeElement.querySelectorAll('.ddz-hand .ddz-card')].map(
    (el) => (el as HTMLElement).textContent?.trim().replace(/\s+/g, '') ?? '',
  );

describe('wakeng strength', () => {
  it('reads 3 as the highest and 4 as the lowest', () => {
    // 与服务端 `WakengRank.Strength` 对齐:3 = 13、2 = 12、A = 11、…、4 = 1。
    expect(wakengStrength(decodeCard(C3)!)).toBe(13);
    expect(wakengStrength(decodeCard(C4)!)).toBe(1);
  });

  it('orders a hand weakest-first, which is the reverse of the encoding for 3', () => {
    const three = decodeCard(C3)!;
    const four = decodeCard(C4)!;

    expect(compareWakengForDisplay(four, three)).toBeLessThan(0);
    // 而按编码序(`rank`)是反的 —— 那正是这个函数存在的理由。
    expect(three.rank - four.rank).toBeLessThan(0);
  });
});

describe('WakengTable', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('shows the hand weakest-first by wakeng strength, not by the encoding', () => {
    // **这条是整个配置存在的理由。** 服务端送来的 `myHand` 是编码顺序(3 在前),
    // 而挖坑里 3 最大 —— 不排的话最强的那张会在最左边。
    const labels = cardLabels(mount(WAKENG_TABLE));

    expect(labels).toHaveLength(2);
    expect(labels[0]).toContain('4');
    expect(labels[1]).toContain('3');
  });

  it('the doudizhu config would order the same hand the other way', () => {
    // 负控制 —— 少了它,一个恒按「4 在前」排的实现在上一条下也是绿的。
    const labels = cardLabels(mount(DOUDIZHU_TABLE));

    expect(labels[0]).toContain('3');
    expect(labels[1]).toContain('4');
  });

  it('draws four face-down kitty cards, not three', () => {
    const fixture = mount(WAKENG_TABLE);

    const backs = fixture.nativeElement.querySelectorAll('.ddz-kitty .ddz-card--back');
    expect(backs.length).toBe(4);
  });

  it('the doudizhu config draws three', () => {
    const fixture = mount(DOUDIZHU_TABLE);

    expect(fixture.nativeElement.querySelectorAll('.ddz-kitty .ddz-card--back').length).toBe(3);
  });

  it('marks the first bidder and the club they showed', () => {
    const fixture = mount(WAKENG_TABLE);

    const marker = fixture.nativeElement.querySelector('[data-testid="first-bidder"]');
    expect(marker).not.toBeNull();
    const text = (marker as HTMLElement).textContent ?? '';

    // **座位号断言不了** —— 测试用的 transloco 没有翻译,所以 `{{seat}}` 不会被插值,
    // 渲染出来的是键本身。所以断言键在,以及**哪一张牌**在:
    expect(text).toContain('cards.wakeng.first-bidder');
    // 服务端点名的是 ♣4,而手里还有一张 3 —— 断言它画的是**被点名的那张**,
    // 而不是从手牌里随便挑一张。
    expect(text).toContain('4');
    expect(text).not.toContain('3');
  });

  it('doudizhu has no first-bidder marker at all', () => {
    // 那个概念在斗地主里不存在 —— 不是「还没算出来」。
    const fixture = mount(DOUDIZHU_TABLE);

    expect(fixture.nativeElement.querySelector('[data-testid="first-bidder"]')).toBeNull();
  });

  it('says 挖 rather than 叫 on the bid buttons', () => {
    const fixture = mount(WAKENG_TABLE);

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('cards.wakeng.bid');
    expect(text).toContain('cards.wakeng.no-bid');
    expect(text).not.toContain('cards.doudizhu.bid');
  });
});
