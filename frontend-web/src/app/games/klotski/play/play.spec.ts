import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type {
  PuzzleAttemptStarted,
  PuzzleHint,
  PuzzleLevelDetail,
  PuzzleSubmitResult,
} from '../../../core/api/models/puzzle.model';
import { PuzzleApiService } from '../../../core/api/puzzle-api.service';
import type { KlotskiLayout, KlotskiMove } from '../model';
import { KlotskiPlay } from './play';

/** Level 0's real shape: 横刀立马 with the two middle soldiers removed. */
const LAYOUT: KlotskiLayout = {
  rows: 5,
  cols: 4,
  name: '初识华容',
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

/**
 * A three-move layout, for the tests that need to reach the end.
 *
 * The real level 0 takes 16 moves — driving that from a spec would be transcribing
 * a solution nobody could check by reading. What the submit path needs proving
 * about is "solving fires exactly one submit carrying every move", and that is the
 * same code on any layout.
 */
const EASY_LAYOUT: KlotskiLayout = {
  rows: 5,
  cols: 4,
  name: '直下',
  exit: { row: 3, col: 1 },
  pieces: [
    { id: 'cao', name: '曹操', row: 0, col: 1, height: 2, width: 2, target: true },
    { id: 'zhang', name: '张飞', row: 0, col: 0, height: 2, width: 1 },
  ],
};

interface Options {
  readonly levelError?: number;
  readonly submitFails?: boolean;
  readonly hint?: KlotskiMove;
  readonly layout?: KlotskiLayout;
}

function setup(options: Options = {}) {
  const submit = vi.fn(() =>
    options.submitFails
      ? throwError(() => new Error('boom'))
      : of({
          isCorrect: true,
          stars: 3,
          durationMs: 1234,
          mistakes: 0,
          hintsUsed: 0,
          newBest: true,
        } as PuzzleSubmitResult),
  );
  const hint = vi.fn(() =>
    of({ revealed: options.hint ?? { id: 'guan', dr: 1, dc: 0 }, hintsUsed: 1 } as PuzzleHint<KlotskiMove>),
  );
  const startAttempt = vi.fn(() =>
    of({
      attemptId: 'attempt-1',
      levelIndex: 0,
      layoutJson: '{}',
      startedAt: '2026-08-16T12:00:00Z',
    } as PuzzleAttemptStarted),
  );

  const api = {
    getLevel: vi.fn(() =>
      options.levelError
        ? throwError(() => ({ status: options.levelError }))
        : of({ levelIndex: 0, difficulty: 1, layoutJson: 'x' } as PuzzleLevelDetail),
    ),
    parseLayout: vi.fn(() => options.layout ?? LAYOUT),
    startAttempt,
    hint,
    submit,
    listLevels: vi.fn(() => of([])),
    getProgress: vi.fn(() => of(null)),
    check: vi.fn(),
  };

  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      KlotskiPlay,
      TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      provideRouter([]),
      { provide: PuzzleApiService, useValue: api },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: { get: () => '0' } } },
      },
    ],
  });

  const fixture = TestBed.createComponent(KlotskiPlay);
  fixture.detectChanges();
  return { fixture, api, submit, hint, startAttempt };
}

type Fixture = ReturnType<typeof setup>['fixture'];

function pieces(fixture: Fixture): HTMLButtonElement[] {
  return Array.from(fixture.nativeElement.querySelectorAll('.kt-piece')) as HTMLButtonElement[];
}

function targets(fixture: Fixture): HTMLButtonElement[] {
  return Array.from(fixture.nativeElement.querySelectorAll('.kt-target')) as HTMLButtonElement[];
}

function piece(fixture: Fixture, name: string): HTMLButtonElement {
  const found = pieces(fixture).find((b) => b.textContent?.trim() === name);
  expect(found, `no piece "${name}"`).toBeTruthy();
  return found!;
}

function click(fixture: Fixture, el: HTMLElement): void {
  el.click();
  fixture.detectChanges();
}

/**
 * 曹操 straight down three times, on EASY_LAYOUT.
 *
 * The piece is picked up **once**: a slide keeps it in hand, because a player
 * pushing a block usually wants to push it again. Re-clicking it would put it down.
 * Destinations are found by grid row so the test does not depend on their order.
 */
function solve(fixture: Fixture): void {
  click(fixture, piece(fixture, '曹操'));
  for (let i = 0; i < 3; i++) {
    const down = targets(fixture).find((b) => (b.style.gridArea ?? '').startsWith(`${i + 2} /`));
    expect(down, `no downward move for 曹操 at step ${i}`).toBeTruthy();
    click(fixture, down!);
  }
}

describe('KlotskiPlay', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('renders the level and opens an attempt', () => {
    const { fixture, startAttempt } = setup();

    expect(pieces(fixture)).toHaveLength(6);
    expect(startAttempt).toHaveBeenCalledWith('klotski', 0);
  });

  it('marks only the legal destinations of the selected piece', () => {
    const { fixture } = setup();

    click(fixture, piece(fixture, '关羽'));

    // 关羽 spans (2,1)-(2,2): 曹操 above, empty below.
    expect(targets(fixture)).toHaveLength(1);
  });

  it('offers nothing for a piece that cannot move', () => {
    const { fixture } = setup();

    click(fixture, piece(fixture, '曹操'));

    expect(targets(fixture)).toHaveLength(0);
  });

  it('counts a move when a piece slides', () => {
    const { fixture } = setup();

    click(fixture, piece(fixture, '关羽'));
    click(fixture, targets(fixture)[0]);

    expect(fixture.nativeElement.textContent).toContain('klotski.moves');
    // 关羽 stays in hand after the slide — a player usually wants to keep pushing
    // the same block — so its new destinations are offered from the new position.
    expect(piece(fixture, '关羽').getAttribute('aria-pressed')).toBe('true');
  });

  it('warns about leaving only after a move has been made', () => {
    const { fixture } = setup();
    // 一步没走的关卡不问 —— 点进去看一眼就走是正常操作,而每次都问会把这个确认框
    // 训练成「闭着眼睛点掉」的东西。
    expect(fixture.componentInstance.leaveWarningKey()).toBeNull();

    click(fixture, piece(fixture, '关羽'));
    click(fixture, targets(fixture)[0]);

    expect(fixture.componentInstance.leaveWarningKey()).toBe('game.leave-confirm.klotski');
  });

  it('never calls check — the client owns the rule', () => {
    // 华容道 has nothing hidden, so a per-move round trip would tell the server
    // nothing it does not learn from replaying the whole path at the end.
    const { fixture, api } = setup({ layout: EASY_LAYOUT });

    solve(fixture);

    expect(api.check).not.toHaveBeenCalled();
  });

  it('submits the whole move list exactly once, when the puzzle is solved', () => {
    const { fixture, submit } = setup({ layout: EASY_LAYOUT });

    solve(fixture);

    expect(submit).toHaveBeenCalledTimes(1);
    const [, payload] = submit.mock.calls[0] as unknown as [string, { moves: KlotskiMove[] }];
    expect(payload.moves).toHaveLength(3);
    expect(payload.moves.at(-1)).toEqual({ id: 'cao', dr: 1, dc: 0 });
  });

  it('shows the stars the server returned rather than working them out', () => {
    const { fixture } = setup({ layout: EASY_LAYOUT });

    solve(fixture);

    expect(fixture.nativeElement.textContent).toContain('klotski.solved');
    expect(fixture.nativeElement.textContent).toContain('klotski.new-best');
  });

  it('offers a retry when submission fails', () => {
    const { fixture } = setup({ submitFails: true, layout: EASY_LAYOUT });

    solve(fixture);

    expect(fixture.nativeElement.textContent).toContain('klotski.error-submit-failed');
    expect(fixture.nativeElement.textContent).toContain('klotski.retry');
  });

  it('never shows the level target while playing', () => {
    // The minimum is the divisor the server scores with. On screen during play it
    // turns a puzzle into a countdown.
    const { fixture } = setup();
    const text = fixture.nativeElement.textContent as string;

    expect(text).not.toContain('minMoves');
    expect(text).not.toMatch(/\b16\b/);
  });

  it('reports the current position when asking for a hint', () => {
    const { fixture, hint } = setup();

    click(fixture, piece(fixture, '关羽'));
    click(fixture, targets(fixture)[0]);
    click(
      fixture,
      Array.from(fixture.nativeElement.querySelectorAll('button')).find((b) =>
        (b as HTMLElement).textContent?.includes('klotski.hint'),
      ) as HTMLElement,
    );

    expect(hint).toHaveBeenCalledTimes(1);
    const [, state] = hint.mock.calls[0] as unknown as [
      string,
      { pieces: { id: string; row: number; col: number }[] },
    ];
    // 关羽 already moved — the server must search from where the player actually is.
    expect(state.pieces.find((p) => p.id === 'guan')).toEqual({ id: 'guan', row: 3, col: 1 });
  });

  it('plays the hinted move and counts it', () => {
    const { fixture } = setup();
    const hintButton = Array.from(fixture.nativeElement.querySelectorAll('button')).find((b) =>
      (b as HTMLElement).textContent?.includes('klotski.hint'),
    ) as HTMLElement;

    click(fixture, hintButton);

    expect(fixture.nativeElement.textContent).toContain('klotski.hints-used');
  });

  it('shows a not-found state for a level that does not exist', () => {
    const { fixture } = setup({ levelError: 404 });

    expect(fixture.nativeElement.textContent).toContain('klotski.level-not-found');
  });

  it('shows a retryable error when the level cannot be loaded', () => {
    const { fixture } = setup({ levelError: 500 });

    expect(fixture.nativeElement.textContent).toContain('klotski.error-load-failed');
    expect(fixture.nativeElement.textContent).toContain('klotski.retry');
  });
});
