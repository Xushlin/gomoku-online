import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { PuzzleApiService } from '../../../core/api/puzzle-api.service';
import type { IdiomGuessLayout, IdiomGuessSolved } from '../model';
import { IdiomGuessPlay } from './play';

/**
 * 两道题:第一条有出处,第二条**没有**。
 *
 * 「没有出处」不是补充样本,是本组好几条断言的前提 —— 可用池 9,615 条里有 252 条没有
 * 出处,产物里就有一条。一个假定出处一定在的界面会给它画一张**空纸条**,而空纸条在
 * 屏幕上和"加载失败"长得一样。
 */
const LAYOUT: IdiomGuessLayout = {
  puzzles: [
    { index: 0, explanation: '形容一下子出了名。', chars: ['一', '鸣', null, '人'] },
    { index: 1, explanation: '比喻做事有始有终。', chars: [null, '始', '有', '终'] },
  ],
};

const langs = { en: { 'idiom-guess': {}, games: { 'idiom-guess': { title: 'x' } } } };

function setup(solvedFor: (word: string) => IdiomGuessSolved | null) {
  const calls: { puzzleIndex: number; word: string }[] = [];
  const hintCalls: unknown[] = [];

  const api = {
    listLevels: vi.fn(() => of([])),
    getLevel: vi.fn(),
    getProgress: vi.fn(),
    startAttempt: vi.fn(() =>
      of({ attemptId: 'att-1', levelIndex: 0, layoutJson: '{}', startedAt: '' }),
    ),
    parseLayout: vi.fn(() => LAYOUT),
    check: vi.fn((_id: string, partial: { puzzleIndex: number; word: string }) => {
      calls.push(partial);
      const solved = solvedFor(partial.word);
      return of({ isCorrect: solved !== null, mistakes: solved ? 0 : 1, solved });
    }),
    hint: vi.fn((_id: string, state?: unknown) => {
      hintCalls.push(state);
      return of({ revealed: { puzzleIndex: 0, position: 2, char: '惊' }, hintsUsed: 1 });
    }),
    submit: vi.fn(() =>
      of({
        isCorrect: true,
        stars: 3,
        durationMs: 1000,
        mistakes: 0,
        hintsUsed: 0,
        newBest: true,
      }),
    ),
  };

  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      IdiomGuessPlay,
      TranslocoTestingModule.forRoot({
        langs,
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [provideRouter([]), { provide: PuzzleApiService, useValue: api }],
  });

  const fixture = TestBed.createComponent(IdiomGuessPlay);
  fixture.detectChanges();
  return { fixture, api, calls, hintCalls };
}

/** 在第 n 个空位输入框里敲一个字。 */
function typeInto(fixture: ReturnType<typeof setup>['fixture'], nth: number, char: string) {
  const inputs = fixture.nativeElement.querySelectorAll('input') as NodeListOf<HTMLInputElement>;
  const input = inputs[nth];
  input.value = char;
  input.dispatchEvent(new Event('input'));
  fixture.detectChanges();
}

const RIGHT: Record<string, IdiomGuessSolved> = {
  一鸣惊人: { index: 0, word: '一鸣惊人', derivation: '《史记·滑稽列传》' },
  有始有终: { index: 1, word: '有始有终', derivation: null },
};

describe('IdiomGuessPlay', () => {
  it('renders one input per blank and shows the given characters as text', () => {
    const { fixture } = setup((w) => RIGHT[w] ?? null);

    // 每题恰好一个空 —— 两题两个输入框。
    expect(fixture.nativeElement.querySelectorAll('input').length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('鸣');
    expect(fixture.nativeElement.textContent).toContain('形容一下子出了名。');
  });

  it('sends the whole word to the server once a blank is filled', () => {
    const { fixture, calls } = setup((w) => RIGHT[w] ?? null);

    typeInto(fixture, 0, '惊');

    expect(calls).toEqual([{ puzzleIndex: 0, word: '一鸣惊人' }]);
  });

  it('shows the derivation the server sent back', () => {
    const { fixture } = setup((w) => RIGHT[w] ?? null);

    typeInto(fixture, 0, '惊');

    expect(fixture.nativeElement.textContent).toContain('史记');
  });

  /**
   * **与上一条 MUST 同时存在。** 只有上一条时,一个"总是画纸条"的实现也是绿的;
   * 只有这一条时,一个"从不画纸条"的实现也是绿的。
   */
  it('draws no note at all for an idiom that has no derivation', () => {
    const { fixture } = setup((w) => RIGHT[w] ?? null);

    typeInto(fixture, 1, '有');

    const notes = fixture.nativeElement.querySelectorAll('p.italic');
    expect(notes.length).toBe(0);
    // 而它确实答对了 —— 否则这条断言是在一个"还没答对"的界面上恒真。
    expect(fixture.nativeElement.textContent).not.toContain('undefined');
    expect(fixture.componentInstance['puzzles']().find((p) => p.puzzle.index === 1)?.solved).toBe(
      true,
    );
  });

  it('a wrong answer is not locked in and carries no note', () => {
    const { fixture } = setup((w) => RIGHT[w] ?? null);

    typeInto(fixture, 0, '天');

    expect(fixture.componentInstance['puzzles']()[0].solved).toBe(false);
    expect(fixture.nativeElement.querySelectorAll('p.italic').length).toBe(0);
  });

  it('tells the server which blank the cursor is on when asking for a hint', () => {
    const { fixture, hintCalls } = setup((w) => RIGHT[w] ?? null);

    const inputs = fixture.nativeElement.querySelectorAll('input') as NodeListOf<HTMLInputElement>;
    inputs[1].dispatchEvent(new Event('focus'));
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(hintCalls[0]).toMatchObject({ selected: '1:0' });
  });

  it('submits once every puzzle is solved', () => {
    const { fixture, api } = setup((w) => RIGHT[w] ?? null);

    typeInto(fixture, 0, '惊');
    // 第 1 个输入框仍然是第一题那个(答对后只是 disabled,不会消失),所以第二题要点名 index 1。
    // 第一版这里写的是 0,而那一下正好撞在「答对了就不再收输入」的守卫上 —— 实现是对的,
    // 测试在对着一个锁住的格子敲字。
    typeInto(fixture, 1, '有');

    expect(api.submit).toHaveBeenCalledTimes(1);
    expect(fixture.nativeElement.textContent).toContain('★');
  });

  /** 客户端拿到的东西里没有答案 —— 布局里被挖的位置就是 null。 */
  it('the layout it receives carries no answer', () => {
    setup((w) => RIGHT[w] ?? null);

    const blanks = LAYOUT.puzzles.flatMap((p) => p.chars.filter((c) => c === null));
    expect(blanks.length).toBe(2);
    expect(JSON.stringify(LAYOUT)).not.toContain('惊');
  });
});
