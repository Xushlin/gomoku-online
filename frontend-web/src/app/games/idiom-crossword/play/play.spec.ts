import { Dialog } from '@angular/cdk/dialog';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { CrosswordLayout, PuzzleCheckResult } from '../../../core/api/models/puzzle.model';
import { PuzzleApiService } from '../../../core/api/puzzle-api.service';
import { Play } from './play';

/**
 * 合而为一 across row 0, 合情合理 down column 0, crossing at (0,0).
 *
 * Everything except the crossing is pre-filled, so a single placement completes
 * **both** slots — which is the case that proves two checks are fired rather
 * than one.
 */
const CROSSING_LAYOUT: CrosswordLayout = {
  rows: 4,
  cols: 4,
  cells: [
    { row: 0, col: 0 },
    { row: 0, col: 1 },
    { row: 0, col: 2 },
    { row: 0, col: 3 },
    { row: 1, col: 0 },
    { row: 2, col: 0 },
    { row: 3, col: 0 },
  ],
  given: [
    { row: 0, col: 1, char: '而' },
    { row: 0, col: 2, char: '为' },
    { row: 0, col: 3, char: '一' },
    { row: 1, col: 0, char: '情' },
    { row: 2, col: 0, char: '合' },
    { row: 3, col: 0, char: '理' },
  ],
  tray: ['合'],
  slots: [
    { index: 0, row: 0, col: 0, direction: 'Horizontal', length: 4 },
    { index: 1, row: 0, col: 0, direction: 'Vertical', length: 4 },
  ],
};

/** One idiom across row 0; nothing pre-filled, so partial placements complete nothing. */
const SINGLE_SLOT_LAYOUT: CrosswordLayout = {
  rows: 1,
  cols: 4,
  cells: [
    { row: 0, col: 0 },
    { row: 0, col: 1 },
    { row: 0, col: 2 },
    { row: 0, col: 3 },
  ],
  given: [],
  tray: ['合', '而', '为', '一'],
  slots: [{ index: 0, row: 0, col: 0, direction: 'Horizontal', length: 4 }],
};

const langs = { en: { 'idiom-crossword': {}, games: { 'idiom-crossword': { title: 'x' } } } };

function setup(layout: CrosswordLayout, check: (n: number) => PuzzleCheckResult) {
  const calls: { slotIndex: number; word: string }[] = [];
  const hintCalls: { id: string; state?: unknown }[] = [];
  let checkCount = 0;

  const api = {
    listLevels: vi.fn(() => of([{ levelIndex: 0 }, { levelIndex: 1 }])),
    startAttempt: vi.fn(() => of({ attemptId: 'att-1', levelIndex: 0, layoutJson: '{}', startedAt: '' })),
    parseLayout: vi.fn(() => layout),
    check: vi.fn((_id: string, partial: { slotIndex: number; word: string }) => {
      calls.push(partial);
      return of(check(checkCount++));
    }),
    // Params are declared so vitest infers the call-arg tuple; the board state
    // the component sends is what the hint-targeting tests assert on.
    hint: vi.fn((id: string, state?: unknown) => {
      hintCalls.push({ id, state });
      return of({ revealed: { row: 0, col: 0, char: '合' }, hintsUsed: 1 });
    }),
    submit: vi.fn(() =>
      of({ isCorrect: false, stars: null, durationMs: null, mistakes: 0, hintsUsed: 0, newBest: false }),
    ),
    getLevel: vi.fn(),
    getProgress: vi.fn(),
  };

  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      Play,
      TranslocoTestingModule.forRoot({
        langs,
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      provideRouter([]),
      { provide: PuzzleApiService, useValue: api },
      { provide: Dialog, useValue: { open: vi.fn(() => ({ closed: of(undefined) })) } },
    ],
  });

  const fixture = TestBed.createComponent(Play);
  fixture.componentRef.setInput('index', '0');
  fixture.detectChanges();

  return { fixture, api, calls, hintCalls, cmp: fixture.componentInstance as unknown as PlayInternals };
}

/** The bits of `Play` these tests drive — protected members, reached deliberately. */
interface PlayInternals {
  /** 公开的:守卫从组件外面调它。 */
  leaveWarningKey(): string | null;
  hint(): void;
  onTileTap(index: number): void;
  onCellTap(key: string): void;
  mistakes(): number;
  hintsUsed(): number;
  board: {
    locked(): ReadonlySet<string>;
    chars(): ReadonlyMap<string, string>;
    usedTiles(): ReadonlySet<number>;
    selected(): string | null;
    select(key: string): void;
  };
}

const CORRECT: PuzzleCheckResult = {
  isCorrect: true,
  mistakes: 0,
  solved: { index: 0, word: '合而为一', explanation: '合成一个整体。' },
};

const WRONG: PuzzleCheckResult = { isCorrect: false, mistakes: 1, solved: null };

describe('Play — check call sequence', () => {
  beforeEach(() => vi.useRealTimers());

  it('fires no check when a placement completes nothing', () => {
    const { cmp, api } = setup(SINGLE_SLOT_LAYOUT, () => CORRECT);

    cmp.onTileTap(0); // one of four cells

    expect(api.check).not.toHaveBeenCalled();
  });

  it('fires exactly one check when a slot becomes full', () => {
    const { cmp, api, calls } = setup(SINGLE_SLOT_LAYOUT, () => CORRECT);

    cmp.onTileTap(0);
    cmp.onTileTap(1);
    cmp.onTileTap(2);
    cmp.onTileTap(3);

    expect(api.check).toHaveBeenCalledTimes(1);
    expect(calls[0]).toEqual({ slotIndex: 0, word: '合而为一' });
  });

  it('fires two checks when one placement completes two crossing slots', () => {
    const { cmp, api, calls } = setup(CROSSING_LAYOUT, () => CORRECT);

    cmp.onTileTap(0); // the single tray tile lands on the crossing

    expect(api.check).toHaveBeenCalledTimes(2);
    expect(calls.map((c) => c.slotIndex).sort()).toEqual([0, 1]);
    expect(calls.map((c) => c.word).sort()).toEqual(['合情合理', '合而为一'].sort());
  });

  it('warns about leaving only after a word has been solved', () => {
    // 需要一个「解出一个词、而关卡还没完」的局面,所以两行各一条成语:第一行只差
    // 一个字,第二行整行空着。CROSSING_LAYOUT 做不到这件事 —— 它那唯一一颗托盘
    // 棋子落在交叉点上,一下把两条都解了,于是网格立刻就满了。
    const TWO_ROW_LAYOUT: CrosswordLayout = {
      rows: 2,
      cols: 4,
      cells: [
        { row: 0, col: 0 },
        { row: 0, col: 1 },
        { row: 0, col: 2 },
        { row: 0, col: 3 },
        { row: 1, col: 0 },
        { row: 1, col: 1 },
        { row: 1, col: 2 },
        { row: 1, col: 3 },
      ],
      given: [
        { row: 0, col: 1, char: '而' },
        { row: 0, col: 2, char: '为' },
        { row: 0, col: 3, char: '一' },
      ],
      tray: ['合', '一', '心', '意'],
      slots: [
        { index: 0, row: 0, col: 0, direction: 'Horizontal', length: 4 },
        { index: 1, row: 1, col: 0, direction: 'Horizontal', length: 4 },
      ],
    };
    const { cmp } = setup(TWO_ROW_LAYOUT, () => CORRECT);
    // 一个词都没解出来的时候不问。
    expect(cmp.leaveWarningKey()).toBeNull();

    cmp.board.select('0,0');
    cmp.onTileTap(0);

    // 前置条件:确实解出了一个词,而关卡确实还没完 —— 否则下面那条断言测的是别的局面。
    expect(cmp.board.locked().has('0,0')).toBe(true);
    expect(cmp.leaveWarningKey()).toBe('game.leave-confirm.crossword');
  });

  it('does not re-check a slot that is already solved', () => {
    const { cmp, api } = setup(CROSSING_LAYOUT, () => CORRECT);

    cmp.onTileTap(0);
    expect(api.check).toHaveBeenCalledTimes(2);

    // Both slots are locked now; nothing further should be asked about.
    cmp.onTileTap(0);
    expect(api.check).toHaveBeenCalledTimes(2);
  });
});

describe('Play — verdict handling', () => {
  it('locks the slot and keeps the solved word on a correct verdict', () => {
    const { cmp } = setup(SINGLE_SLOT_LAYOUT, () => CORRECT);

    cmp.onTileTap(0);
    cmp.onTileTap(1);
    cmp.onTileTap(2);
    cmp.onTileTap(3);

    for (const key of ['0,0', '0,1', '0,2', '0,3']) {
      expect(cmp.board.locked().has(key)).toBe(true);
    }
    // The tiles stay put — a solved idiom is not returnable.
    expect(cmp.board.chars().get('0,0')).toBe('合');
  });

  it('takes the mistake count from the server, not from a local counter', async () => {
    // The server says 7; a client that counted its own would say 1.
    const { cmp } = setup(SINGLE_SLOT_LAYOUT, () => ({ ...WRONG, mistakes: 7 }));

    cmp.onTileTap(0);
    cmp.onTileTap(1);
    cmp.onTileTap(2);
    cmp.onTileTap(3);

    expect(cmp.mistakes()).toBe(7);
  });

  it('returns the tiles after the shake on a wrong verdict', async () => {
    vi.useFakeTimers();
    const { cmp } = setup(SINGLE_SLOT_LAYOUT, () => WRONG);

    cmp.onTileTap(0);
    cmp.onTileTap(1);
    cmp.onTileTap(2);
    cmp.onTileTap(3);

    // Still on the board while it shakes.
    expect(cmp.board.chars().size).toBe(4);

    vi.advanceTimersByTime(500);

    expect(cmp.board.chars().size).toBe(0);
    expect(cmp.board.usedTiles().size).toBe(0);
    vi.useRealTimers();
  });

  it('leaves a wrong slot unlocked so it can be retried', async () => {
    vi.useFakeTimers();
    const { cmp } = setup(SINGLE_SLOT_LAYOUT, () => WRONG);

    cmp.onTileTap(0);
    cmp.onTileTap(1);
    cmp.onTileTap(2);
    cmp.onTileTap(3);
    vi.advanceTimersByTime(500);

    expect(cmp.board.locked().size).toBe(0);
    vi.useRealTimers();
  });
});

describe('Play — hint targeting', () => {
  it('sends the board state so the server can aim the hint', () => {
    const { cmp, api, hintCalls } = setup(SINGLE_SLOT_LAYOUT, () => CORRECT);

    cmp.onTileTap(0); // fills 0,0
    cmp.hint();

    expect(api.hint).toHaveBeenCalledTimes(1);
    const state = hintCalls[0].state as { filled: string[]; selected: string | null };
    // The whole point of the fix: the server is told what is already filled, so
    // it does not spend the hint re-revealing a cell the player has solved.
    expect(state.filled).toContain('0,0');
    expect(state.selected).toBe('0,1');
  });

  it('takes hintsUsed from the response rather than counting locally', () => {
    const { cmp, api } = setup(SINGLE_SLOT_LAYOUT, () => CORRECT);
    api.hint.mockReturnValue(of({ revealed: { row: 0, col: 2, char: '为' }, hintsUsed: 7 }));

    cmp.hint();

    expect(cmp.hintsUsed()).toBe(7);
  });
});
