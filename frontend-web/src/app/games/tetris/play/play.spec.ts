import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { PagedResult } from '../../../core/api/models/leaderboard.model';
import type {
  ScoreLeaderboardEntry,
  ScoreRunResult,
  ScoreRunStarted,
  SubmitScoreRunBody,
} from '../../../core/api/models/score-run.model';
import { ScoreRunsApiService } from '../../../core/api/score-runs-api.service';
import { TetrisPlay } from './play';

const SEED = 20260818;

/**
 * Real strings rather than an empty bag. With empty `langs` the pipe echoes the key
 * and **interpolation never runs** — so the assertion that the final score is the
 * server's would have been checking a literal `{{score}}`.
 */
const langs = {
  en: {
    tetris: {
      start: 'Start a run',
      starting: 'Asking the server',
      retry: 'Try again',
      submitting: 'Scoring',
      'play-again': 'Play again',
      pause: 'Pause',
      resume: 'Resume',
      score: 'Score',
      lines: 'Lines',
      level: 'Level',
      next: 'Next',
      subtitle: 'sub',
      keys: 'keys',
      'no-tuck': 'no tuck',
      'scores-link': 'High scores',
      'final-score': '{{score}} points',
      'final-detail': '{{lines}} lines, level {{level}}',
      'server-confirmed': 'confirmed by the server',
      'no-placements': 'No piece was placed',
      'control-left': 'Move left',
      'control-right': 'Move right',
      'control-rotate': 'Rotate',
      'control-soft-drop': 'Soft drop',
      'control-hard-drop': 'Hard drop',
      'error-start-failed': 'Could not start',
      'error-submit-failed': 'Scoring failed',
    },
    games: { tetris: { title: '俄罗斯方块' } },
  },
};

class StubApi extends ScoreRunsApiService {
  startCalls: string[] = [];
  submitted: { runId: string; body: SubmitScoreRunBody }[] = [];
  failStart = false;
  failSubmit = false;
  /** What the server says the run scored. Deliberately not the client's number. */
  serverResult: ScoreRunResult = {
    runId: 'run-1',
    score: 4242,
    lines: 7,
    level: 2,
    placements: 3,
    durationMs: 1234,
  };

  start(gameKey: string) {
    this.startCalls.push(gameKey);
    if (this.failStart) return throwError(() => new Error('nope'));
    return of<ScoreRunStarted>({
      runId: 'run-1',
      gameKey,
      seed: SEED,
      startedAt: '2026-08-18T10:00:00Z',
    });
  }

  submit(runId: string, body: SubmitScoreRunBody) {
    this.submitted.push({ runId, body });
    if (this.failSubmit) return throwError(() => new Error('nope'));
    return of(this.serverResult);
  }

  leaderboard() {
    return of<PagedResult<ScoreLeaderboardEntry>>({ items: [], total: 0, page: 1, pageSize: 20 });
  }
}

describe('TetrisPlay', () => {
  let api: StubApi;
  let fixture: ComponentFixture<TetrisPlay>;

  beforeEach(() => {
    api = new StubApi();
    TestBed.configureTestingModule({
      imports: [
        TetrisPlay,
        TranslocoTestingModule.forRoot({
          langs,
          translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
          preloadLangs: true,
        }),
      ],
      providers: [provideRouter([]), { provide: ScoreRunsApiService, useValue: api }],
    });
    fixture = TestBed.createComponent(TetrisPlay);
    fixture.detectChanges();
  });

  const text = () => fixture.nativeElement.textContent as string;
  const click = (label: string): void => {
    const buttons = [...fixture.nativeElement.querySelectorAll('button')] as HTMLButtonElement[];
    const button = buttons.find((b) => (b.textContent ?? '').includes(label));
    if (!button) throw new Error(`no button matching "${label}"`);
    button.click();
    fixture.detectChanges();
  };
  const filledCells = () =>
    [...fixture.nativeElement.querySelectorAll('app-tetris-board div div')].filter((el) =>
      (el as HTMLElement).className.includes('bg-primary'),
    ).length;

  it('shows no piece before a run is started', () => {
    // The seed decides the sequence, so there is nothing to draw until it arrives.
    expect(api.startCalls).toEqual([]);
    expect(filledCells()).toBe(0);
  });

  it('asks the server for a run, with no seed of its own', () => {
    click('Start a run');

    expect(api.startCalls).toEqual(['tetris']);
  });

  it('draws a piece once the seed arrives', () => {
    click('Start a run');

    expect(filledCells()).toBeGreaterThan(0);
  });

  it('renders no piece and an error when the run cannot be started', () => {
    api.failStart = true;

    click('Start a run');

    // No offline fallback: a locally seeded run has nowhere to be submitted, and
    // the player would only find out at the end.
    expect(filledCells()).toBe(0);
    expect(text()).toContain('Could not start');
  });

  it('submits the placements it recorded, and nothing else', () => {
    click('Start a run');
    click('Hard drop');
    // Force the field to top out so the run ends and submits.
    for (let i = 0; i < 400 && api.submitted.length === 0; i++) {
      click('Hard drop');
    }

    expect(api.submitted).toHaveLength(1);
    const body = api.submitted[0].body;
    expect(body.placements.length).toBeGreaterThan(0);
    // Score / lines / level / duration are server facts and have no field here.
    expect(Object.keys(body)).toEqual(['placements']);
    for (const p of body.placements) {
      expect(Object.keys(p).sort()).toEqual(['column', 'rotation']);
    }
  });

  it('shows the score the server returned, not the one it was showing', () => {
    click('Start a run');
    for (let i = 0; i < 400 && api.submitted.length === 0; i++) {
      click('Hard drop');
    }
    fixture.detectChanges();

    // 4242 is not a score this field can produce — that is the point. The running
    // number on screen was a preview; the recorded one is the server's.
    expect(text()).toContain('4242 points');
  });

  it('shows no score at all when submitting fails', () => {
    api.failSubmit = true;

    click('Start a run');
    for (let i = 0; i < 400 && api.submitted.length === 0; i++) {
      click('Hard drop');
    }
    fixture.detectChanges();

    expect(text()).toContain('Scoring failed');
    expect(text()).not.toContain('4242');
  });

  it('consumes the arrow keys and space so the page does not scroll', () => {
    click('Start a run');

    for (const key of ['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', ' ']) {
      const event = new KeyboardEvent('keydown', { key, cancelable: true });
      const spy = vi.spyOn(event, 'preventDefault');
      document.dispatchEvent(event);
      expect(spy, key).toHaveBeenCalled();
    }
  });

  it('moves the piece with the arrow keys', () => {
    click('Start a run');
    const before = activeColumns();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowLeft' }));
    fixture.detectChanges();

    expect(activeColumns()).not.toEqual(before);
  });

  it('pauses and resumes with P', () => {
    click('Start a run');

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'p' }));
    fixture.detectChanges();
    expect(text()).toContain('Resume');

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'p' }));
    fixture.detectChanges();
    expect(text()).toContain('Pause');
  });

  it('ignores movement while paused', () => {
    click('Start a run');
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'p' }));
    fixture.detectChanges();
    const before = activeColumns();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowLeft' }));
    fixture.detectChanges();

    expect(activeColumns()).toEqual(before);
  });

  it('offers touch controls, as real buttons', () => {
    click('Start a run');
    const labels = ['Move left', 'Move right', 'Rotate', 'Soft drop'];

    for (const label of labels) {
      const button = fixture.nativeElement.querySelector(`button[aria-label="${label}"]`);
      expect(button, label).toBeTruthy();
      expect(button.tagName).toBe('BUTTON');
    }
  });

  /** Which columns the active (lighter) cells are in — enough to see a move happen. */
  function activeColumns(): string {
    return [...fixture.nativeElement.querySelectorAll('app-tetris-board div div')]
      .map((el, i) => ((el as HTMLElement).className.includes('bg-primary/70') ? i : -1))
      .filter((i) => i >= 0)
      .join(',');
  }
});
