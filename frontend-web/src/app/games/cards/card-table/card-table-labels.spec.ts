import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { describe, expect, it } from 'vitest';
import en from '../../../../../public/i18n/en.json';
import zhCN from '../../../../../public/i18n/zh-CN.json';
import type { RoomState } from '../../../core/api/models/room.model';
import { DOUDIZHU_TABLE } from '../../doudizhu/seat-view';
import { WAKENG_TABLE } from '../../wakeng/seat-view';
import type { CardTableConfig } from '../card-table-config';
import { CardTable } from './card-table';

/**
 * 牌桌的标签必须**真的翻出来**,而不是显示拼出来的原始键。
 *
 * **这条测试是一个真缺陷换来的。** 斗地主的 `i18nPrefix` 写的是 `game.doudizhu`,而
 * 键在 `cards.doudizhu.*` —— 错一个词,于是叫分那一排三个按钮全都显示
 * `game.doudizhu.bid`,**玩家分不出哪个是叫几分**;地主徽标同样是原始键。
 *
 * 为什么别的测试都没红:
 *
 * - `card-table.spec.ts` 挂的是**空翻译树**(`langs: { en: {} }`),所以在那里「显示
 *   原始键」是常态,任何断言都不会因此变红。
 * - 双语对齐那条测试只比 en / zh 的**键集合是否相等** —— 两边同样都没有
 *   `game.doudizhu.*`,所以它是绿的。
 *
 * 所以这里挂**真的 `en.json` / `zh-CN.json`**,判据两条,而两条都量过会红:
 *
 * 1. 每份配置的 `i18nPrefix` 在两个语言文件里都指向一棵非空子树;
 * 2. 叫分那一排上**真的画出了语言文件里那句译文**(按前缀查出来,不是写死的英文)。
 */
/** 按点号路径取值 —— 用来从真语言文件里读出「本该显示什么」。 */
function lookup(tree: unknown, path: string): unknown {
  return path.split('.').reduce<unknown>((acc, part) => (acc as Record<string, unknown>)?.[part], tree);
}

const CONFIGS: readonly { name: string; config: CardTableConfig }[] = [
  { name: 'doudizhu', config: DOUDIZHU_TABLE },
  { name: 'wakeng', config: WAKENG_TABLE },
];

/** 叫分阶段 —— 叫分按钮、角色徽标、首叫者提示都在这个阶段出现。 */
function biddingView(): string {
  return JSON.stringify({
    phase: 'Bidding',
    landlord: null,
    baseScore: 0,
    bidsMade: 1,
    myHand: 'ABC',
    handCounts: [3, 17, 17],
    kitty: null,
    tableSeat: null,
    tableCards: null,
    firstBidder: { seat: 1 },
    highBid: { seat: 1, points: 1 },
    canFollow: true,
  });
}

function room(view: string, gameKey: string): RoomState {
  return {
    id: 'r-1',
    name: 'table',
    gameKey,
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
      currentSeat: 0,
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
  template: `<app-card-table [state]="state()" [config]="config()" [mySeat]="0" />`,
})
class Host {
  readonly state = signal<RoomState | null>(null);
  readonly config = signal<CardTableConfig>(DOUDIZHU_TABLE);
}

function mount(locale: 'en' | 'zh-CN', config: CardTableConfig, gameKey: string) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      Host,
      TranslocoTestingModule.forRoot({
        // 真的语言文件,不是 fixture —— 那正是这条测试的全部意义。
        langs: { en: en as Record<string, unknown>, 'zh-CN': zhCN as Record<string, unknown> },
        translocoConfig: {
          availableLangs: ['en', 'zh-CN'],
          defaultLang: locale,
          /*
           * 和生产同一套缺失键处理。**不配它,这条测试会为了错的理由变绿:**
           * `TranslocoTestingModule` 默认对缺失键渲染的不是键本身,于是「牌桌上出现
           * 原始键」在测试里根本观察不到 —— 而那正是真浏览器里发生的事。变异证明的:
           * 把前缀改回 `game.doudizhu`,只有「前缀存在」那条红了。
           */
          missingHandler: { useFallbackTranslation: false, logMissingKey: false },
          prodMode: false,
        },
        preloadLangs: true,
      }),
    ],
  });
  const fixture = TestBed.createComponent(Host);
  fixture.componentInstance.config.set(config);
  fixture.componentInstance.state.set(room(biddingView(), gameKey));
  fixture.detectChanges();
  return fixture;
}

describe('card table labels resolve against the shipped locale files', () => {
  for (const { name, config } of CONFIGS) {
    for (const locale of ['en', 'zh-CN'] as const) {
      it(`${name} in ${locale} shows the real ${locale}.json text on the bid row`, () => {
        const fixture = mount(locale, config, name);
        // `innerText` 在 jsdom 里是空的 —— 那正是上一版负向断言恒真的原因之一。
        const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

        // 前置条件:确实画出了叫分那一排。
        const buttons = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')];
        expect(buttons.length).toBeGreaterThan(3);

        /*
         * 判据是**正向**的:从语言文件里按这份配置的前缀取出译文,断言它真的画在了
         * 屏幕上。前缀写错时那句译文取不到、也画不出来 —— 这一条会红。
         *
         * 原来写的是负向的「渲染结果里不含前缀」,而它**恒真**:jsdom +
         * TranslocoTestingModule 对缺失键渲染的不是键本身(真浏览器里是),装上生产
         * 那套 missingHandler 也一样。**一条测不出错的断言比没有断言更糟**,因为它
         * 看起来在保护。变异证明的:改坏前缀之后只有下面那条「前缀存在」红了。
         */
        const expected = lookup(locale === 'en' ? en : zhCN, `${config.i18nPrefix}.no-bid`);
        expect(typeof expected, `${config.i18nPrefix}.no-bid missing from ${locale}.json`).toBe('string');
        expect(text).toContain(expected as string);
      });
    }
  }

  it('every config points at a prefix that exists in both locales', () => {
    for (const { name, config } of CONFIGS) {
      for (const [locale, tree] of [
        ['en', en],
        ['zh-CN', zhCN],
      ] as const) {
        const node = config.i18nPrefix
          .split('.')
          .reduce<unknown>((acc, part) => (acc as Record<string, unknown>)?.[part], tree);
        expect(node, `${name}: ${config.i18nPrefix} missing from ${locale}.json`).toBeTruthy();
        expect(Object.keys(node as object).length, `${name}: ${locale} subtree empty`).toBeGreaterThan(0);
      }
    }
  });
});
