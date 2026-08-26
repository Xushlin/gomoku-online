import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it } from 'vitest';
import { ManualApiService } from '../../../core/api/manual-api.service';
import type { ManualCatalogue as Catalogue, ManualLine } from '../../../core/api/models/manual.model';
import { GameCapabilitiesService } from '../../game-capabilities.service';
import { StubGameCapabilities } from '../../game-capabilities.stub';
import { LanguageService } from '../../../core/i18n/language.service';
import { ManualCatalogue } from './manual-catalogue/manual-catalogue';
import { ManualStudy } from './manual-study/manual-study';

/** 真实文案 —— 用真语言文件挂载,好让「界面上不出现将死」是个正向断言。 */
const zh = {
  manual: {
    meihuapu: { title: '梅花谱', subtitle: '古谱' },
    chapter: '第 {{n}} 局',
    'line-count': '共 {{count}} 条变化',
    moves: '{{count}} 手',
    study: '研习',
    loading: '正在载入…',
    'not-found': '没有这部谱',
    'line-not-found': '没有这条变化',
    'error-load-failed': '载入失败',
    retry: '重试',
    'back-to-catalogue': '返回目录',
    'verdict-label': '谱评',
    verdict: { red: '红优', black: '黑优' },
    'ply-of': '第 {{current}} / {{total}} 手',
    'no-commentary': '本条暂无注解。',
    entry: '梅花谱',
  },
};

function stubApi(over: Partial<ManualApiService> = {}): ManualApiService {
  return {
    getCatalogue: () => of(CATALOGUE),
    getLine: () => of(LINE),
    ...over,
  } as ManualApiService;
}

const CATALOGUE: Catalogue = {
  manualKey: 'meihuapu',
  gameKey: 'xiangqi',
  chapters: [
    {
      chapter: 1,
      lines: [
        { id: 11, title: '第1局取中兵压马破上右士', moveCount: 46, winnerSeat: 1 },
        { id: 12, title: '第1局巡河炮攻横车尾随捉马', moveCount: 31, winnerSeat: 0 },
      ],
    },
    { chapter: 7, lines: [{ id: 71, title: '第7局飞象进马破过河车边马', moveCount: 28, winnerSeat: 1 }] },
  ],
};

/** 前四手取自实测过的第 1 局。 */
const LINE: ManualLine = {
  id: 11,
  manualKey: 'meihuapu',
  gameKey: 'xiangqi',
  chapter: 1,
  title: '第1局取中兵压马破上右士',
  winnerSeat: 1,
  moves: [
    { ply: 1, fromRow: 9, fromCol: 6, row: 7, col: 4, seat: 0 },
    { ply: 2, fromRow: 2, fromCol: 7, row: 2, col: 4, seat: 1 },
    { ply: 3, fromRow: 9, fromCol: 7, row: 7, col: 8, seat: 0 },
    { ply: 4, fromRow: 0, fromCol: 7, row: 2, col: 6, seat: 1 },
  ],
};

function configure(api: ManualApiService, extraProviders: unknown[] = []): void {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      TranslocoTestingModule.forRoot({
        langs: { 'zh-CN': zh },
        translocoConfig: { availableLangs: ['zh-CN'], defaultLang: 'zh-CN' },
        preloadLangs: true,
      }),
    ],
    providers: [
      provideRouter([]),
      { provide: ManualApiService, useValue: api },
      { provide: LanguageService, useValue: { current: signal('zh-CN') } },
      {
        provide: GameCapabilitiesService,
        useValue: StubGameCapabilities.sized({ xiangqi: { rows: 10, cols: 9 } }),
      },
      ...(extraProviders as never[]),
    ],
  });
}

describe('ManualCatalogue', () => {
  beforeEach(() => TestBed.resetTestingModule());

  const mount = (api: ManualApiService = stubApi()) => {
    configure(api);
    const fixture = TestBed.createComponent(ManualCatalogue);
    fixture.detectChanges();
    return fixture;
  };

  const text = (fixture: ReturnType<typeof mount>) =>
    (fixture.nativeElement as HTMLElement).textContent ?? '';

  /**
   * 分组数**来自数据**。硬编码「8 局」会在下一部谱落地那天静静对不上,而这条断言
   * 用的是一个只有两局的目录 —— 若有人写死 8,它会红。
   */
  it('renders exactly the chapters the server sent', () => {
    const fixture = mount();
    const headings = (fixture.nativeElement as HTMLElement).querySelectorAll('h2');
    expect(headings).toHaveLength(2);
    expect([...headings].map((h) => h.textContent?.trim())).toEqual(['第 1 局', '第 7 局']);
  });

  it('lists every variation with its half-move count and the verdict', () => {
    const fixture = mount();
    const items = (fixture.nativeElement as HTMLElement).querySelectorAll('li');
    expect(items).toHaveLength(3);
    const body = text(fixture);
    expect(body).toContain('第1局取中兵压马破上右士');
    expect(body).toContain('46 手');
    // 两种评断都在样本里 —— 否则「按评断显示」会在单一取值上恒真。
    expect(body).toContain('黑优');
    expect(body).toContain('红优');
  });

  it('counts the lines from the data, not from a constant', () => {
    const fixture = mount();
    expect(text(fixture)).toContain('共 3 条变化');
  });

  /**
   * 375 px 的**机制**:单列 + 标题可断行。
   *
   * jsdom 没有布局引擎,`scrollWidth` 在这里是 0,所以**溢出量不到** —— 本仓库既有的
   * 375 px 断言也是这么写的(见 `header.responsive.spec.ts` 的说明)。这条钉的是两件
   * 会导致溢出的事:栅格在 `sm` 以下是一列,而最长的局名带 `break-words`。
   *
   * 真正的像素测量要在一个**显示着的**浏览器面板里做,而这次没做到 —— 面板全程隐藏,
   * `innerWidth` 是 0。这一点写在这里,而不是假装量过了。
   */
  it('stays single-column below sm and lets long titles wrap', () => {
    const fixture = mount();
    const grids = [...(fixture.nativeElement as HTMLElement).querySelectorAll('ul')];
    expect(grids.length).toBeGreaterThan(0);
    for (const ul of grids) {
      expect(ul.className).toContain('grid-cols-1');
      expect(ul.className).toContain('sm:grid-cols-2');
    }
    const titles = [...(fixture.nativeElement as HTMLElement).querySelectorAll('li > span')];
    const longest = titles.reduce((a, b) =>
      (a.textContent ?? '').length >= (b.textContent ?? '').length ? a : b,
    );
    expect((longest.textContent ?? '').length).toBeGreaterThanOrEqual(12);
    expect(longest.className).toContain('break-words');
  });

  it('shows a not-found state on 404 rather than an empty catalogue', () => {
    const fixture = mount(
      stubApi({
        getCatalogue: () =>
          throwError(() => new HttpErrorResponse({ status: 404 })),
      }),
    );
    expect(text(fixture)).toContain('没有这部谱');
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('h2')).toHaveLength(0);
  });

  it('offers a retry on a transport error', () => {
    let calls = 0;
    const fixture = mount(
      stubApi({
        getCatalogue: () => {
          calls += 1;
          return calls === 1
            ? throwError(() => new HttpErrorResponse({ status: 500 }))
            : of(CATALOGUE);
        },
      }),
    );
    expect(text(fixture)).toContain('载入失败');
    (fixture.nativeElement as HTMLElement).querySelector('button')!.click();
    fixture.detectChanges();
    expect(calls).toBe(2);
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('h2')).toHaveLength(2);
  });
});

describe('ManualStudy', () => {
  beforeEach(() => TestBed.resetTestingModule());

  const mount = (api: ManualApiService = stubApi(), lineId = '11') => {
    configure(api, [
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map([['lineId', lineId]]) } } },
    ]);
    const fixture = TestBed.createComponent(ManualStudy);
    fixture.detectChanges();
    return fixture;
  };

  const host = (fixture: ReturnType<typeof mount>) => fixture.nativeElement as HTMLElement;
  const comp = (fixture: ReturnType<typeof mount>) =>
    fixture.componentInstance as unknown as {
      onScrub: (n: number) => void;
      currentPly: () => number;
      totalMoves: () => number;
    };

  it('starts at the opening position — 32 pieces, no moves played', () => {
    const fixture = mount();
    expect(host(fixture).querySelectorAll('.xq-piece')).toHaveLength(32);
    expect(comp(fixture).currentPly()).toBe(0);
    expect(comp(fixture).totalMoves()).toBe(4);
  });

  /**
   * **子要真的挪了。**
   *
   * 第一版这条只断言前后都是 32 个子 —— 而这条线路的前四手一个子都不吃,所以它在
   * 「棋盘完全没动」时同样是绿的。改成看具体格子:第 1 手是 (9,6) → (7,4)。
   */
  it('moves the piece to the destination and puts it back on the way out', () => {
    const fixture = mount();
    const points = () => host(fixture).querySelectorAll('.xq-point');
    const hasPiece = (row: number, col: number) =>
      points()[row * 9 + col].querySelector('.xq-piece') !== null;

    // 正面对照:坐标索引成立的前提是这 90 个点按行铺开。
    expect(points()).toHaveLength(90);

    expect(hasPiece(9, 6)).toBe(true);
    expect(hasPiece(7, 4)).toBe(false);

    comp(fixture).onScrub(1);
    fixture.detectChanges();
    expect(hasPiece(9, 6)).toBe(false);
    expect(hasPiece(7, 4)).toBe(true);

    comp(fixture).onScrub(0);
    fixture.detectChanges();
    expect(hasPiece(9, 6)).toBe(true);
    expect(hasPiece(7, 4)).toBe(false);
  });

  it('clamps a seek beyond either end', () => {
    const fixture = mount();
    comp(fixture).onScrub(999);
    expect(comp(fixture).currentPly()).toBe(4);
    comp(fixture).onScrub(-3);
    expect(comp(fixture).currentPly()).toBe(0);
  });

  /**
   * **谱评,不是将死。** 31 条线路里 20 条走到「优势已成」就停,把评断说成将死在那 20 条
   * 上是错的,而错的样子和对的样子在界面上完全一样 —— 所以这条是正向断言:那句话必须
   * 是「谱评 / 黑优」,而 MUST NOT 出现终局类词。
   */
  it('presents the outcome as the manual verdict, never as a mate', () => {
    const fixture = mount();
    const body = host(fixture).textContent ?? '';
    expect(body).toContain('谱评');
    expect(body).toContain('黑优');
    for (const word of ['将死', '绝杀', '杀棋', 'checkmate', 'Checkmate']) {
      expect(body).not.toContain(word);
    }
  });

  it('shows the shared scrubber and no controls of its own', () => {
    const fixture = mount();
    expect(host(fixture).querySelectorAll('app-move-scrubber')).toHaveLength(1);
    expect(host(fixture).querySelectorAll('input[type="range"]')).toHaveLength(1);
  });

  /**
   * 注解区高度稳定的**机制**是一条 min-height。
   *
   * jsdom 不做布局,所以这里量不到真实高度 —— 这条断言是围栏,真正的检查在浏览器里
   * (走一遍全谱,看棋盘有没有上下弹)。把它写成断言而不是注释,是因为 v1 一条注解都
   * 没有,而「以后一半的手上有字」是这块区域唯一会变的方向。
   */
  it('reserves height for the commentary block', () => {
    const fixture = mount();
    const block = [...host(fixture).querySelectorAll('div')].find((d) =>
      (d.textContent ?? '').includes('谱评'),
    );
    expect(block).toBeDefined();
    expect(block!.className).toMatch(/min-h-/);
  });

  it('shows a not-found state for a line id that does not exist', () => {
    const fixture = mount(
      stubApi({ getLine: () => throwError(() => new HttpErrorResponse({ status: 404 })) }),
    );
    expect(host(fixture).textContent).toContain('没有这条变化');
  });

  it('shows not-found for a non-numeric line id without calling the API', () => {
    let called = false;
    const fixture = mount(
      stubApi({
        getLine: () => {
          called = true;
          return of(LINE);
        },
      }),
      'abc',
    );
    expect(called).toBe(false);
    expect(host(fixture).textContent).toContain('没有这条变化');
  });
});
