import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it } from 'vitest';
import type { PagedResult } from '../../../core/api/models/leaderboard.model';
import type {
  ScoreLeaderboardEntry,
  ScoreRunResult,
  ScoreRunStarted,
  ScoreWindow,
} from '../../../core/api/models/score-run.model';
import { ScoreRunsApiService } from '../../../core/api/score-runs-api.service';
import { GameCatalogService } from '../../../games/game-catalog.service';
import { ScoreLeaderboardPage } from './score-leaderboard-page';

/**
 * Real strings rather than an empty bag, so the assertions read the same text a
 * player would. With empty `langs` the pipe echoes the key and interpolation never
 * runs — which would leave `{{page}}`-style bugs invisible.
 */
const langs = {
  en: {
    scores: {
      title: '{{game}} high scores',
      'back-to-game': 'Back to the game',
      'window-group': 'Time range',
      'window-week': 'This week',
      'window-month': 'This month',
      'window-all': 'All time',
      loading: 'Loading',
      'load-failed': 'Could not load the board.',
      retry: 'Try again',
      empty: 'Nobody has played in this range yet.',
      caption: 'High scores',
      rank: 'Rank',
      player: 'Player',
      score: 'Score',
      lines: 'Lines',
      level: 'Level',
      prev: 'Previous',
      next: 'Next',
      'page-of': 'Page {{page}} of {{total}}',
    },
    games: { tetris: { title: '俄罗斯方块' } },
  },
};

function entry(rank: number, name: string, score: number): ScoreLeaderboardEntry {
  return {
    rank,
    userId: `u-${rank}`,
    username: name,
    score,
    lines: score / 100,
    level: 1,
    finishedAt: '2026-08-18T10:00:00Z',
  };
}

class StubApi extends ScoreRunsApiService {
  calls: { gameKey: string; window: ScoreWindow; page: number }[] = [];
  items: readonly ScoreLeaderboardEntry[] = [entry(1, 'Alice', 300), entry(2, 'Bob', 100)];
  total = 2;
  fail = false;

  start() {
    return of({} as ScoreRunStarted);
  }

  submit() {
    return of({} as ScoreRunResult);
  }

  leaderboard(gameKey: string, window: ScoreWindow, page: number, pageSize: number) {
    this.calls.push({ gameKey, window, page });
    if (this.fail) return throwError(() => new Error('nope'));
    return of<PagedResult<ScoreLeaderboardEntry>>({
      items: this.items,
      total: this.total,
      page,
      pageSize,
    });
  }
}

describe('ScoreLeaderboardPage', () => {
  let api: StubApi;
  let fixture: ComponentFixture<ScoreLeaderboardPage>;

  function create(gameKey = 'tetris'): void {
    TestBed.configureTestingModule({
      imports: [
        ScoreLeaderboardPage,
        TranslocoTestingModule.forRoot({
          langs,
          translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
          preloadLangs: true,
        }),
      ],
      providers: [
        provideRouter([]),
        { provide: ScoreRunsApiService, useValue: api },
        {
          provide: GameCatalogService,
          useValue: {
            all: () => [],
            available: () => [],
            planned: () => [],
            byKey: (k: string) =>
              k === 'tetris'
                ? { key: k, titleKey: 'games.tetris.title', launchRoute: '/g/tetris' }
                : undefined,
          },
        },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => gameKey } } } },
      ],
    });
    fixture = TestBed.createComponent(ScoreLeaderboardPage);
    fixture.detectChanges();
  }

  beforeEach(() => {
    api = new StubApi();
  });

  const text = () => fixture.nativeElement.textContent as string;
  const rows = () => [...fixture.nativeElement.querySelectorAll('tbody tr')];
  const clickWindow = (label: string): void => {
    const buttons = [...fixture.nativeElement.querySelectorAll('button')] as HTMLButtonElement[];
    const button = buttons.find((b) => (b.textContent ?? '').includes(label));
    if (!button) throw new Error(`no button matching "${label}"`);
    button.click();
    fixture.detectChanges();
  };

  it('opens on the natural week', () => {
    create();

    expect(api.calls[0]).toMatchObject({ gameKey: 'tetris', window: 'week', page: 1 });
  });

  it('renders one row per player with the server rank', () => {
    create();

    expect(rows()).toHaveLength(2);
    expect(text()).toContain('Alice');
    expect(text()).toContain('300');
  });

  it('reloads with the chosen window', () => {
    create();

    clickWindow('All time');

    expect(api.calls.at(-1)).toMatchObject({ window: 'all', page: 1 });
  });

  it('does not refetch when the current window is clicked again', () => {
    create();
    const before = api.calls.length;

    clickWindow('This week');

    expect(api.calls.length).toBe(before);
  });

  it('treats an empty board as empty, not as an error', () => {
    // A key nobody has played comes back 200 + empty. "Nobody has played this yet"
    // is a fact, not a fault — and on a collection endpoint the caller cannot tell
    // it apart from "this game has no board".
    api.items = [];
    api.total = 0;

    create('not-a-game');

    expect(text()).toContain('Nobody has played in this range yet.');
    expect(text()).not.toContain('Could not load the board.');
  });

  it('shows a retryable error when the request fails', () => {
    api.fail = true;

    create();

    expect(text()).toContain('Could not load the board.');
    expect(rows()).toHaveLength(0);
  });

  it('keeps a long username from widening the page', () => {
    // jsdom has no layout, so this cannot assert pixels. What it *can* pin is the
    // structure that makes the CSS work: the table lives in its own
    // `overflow-x-auto` box, so a long row scrolls inside it rather than pushing
    // the page sideways. The pixel check belongs in a browser run.
    api.items = [entry(1, 'A'.repeat(120), 300)];
    api.total = 1;

    create();

    const scroller = fixture.nativeElement.querySelector('.overflow-x-auto');
    expect(scroller).toBeTruthy();
    expect(scroller.querySelector('table')).toBeTruthy();
  });
});
