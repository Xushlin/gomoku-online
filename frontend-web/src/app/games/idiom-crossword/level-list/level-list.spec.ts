import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import type { PuzzleLevelSummary } from '../../../core/api/models/puzzle.model';
import { PuzzleApiService } from '../../../core/api/puzzle-api.service';
import { LevelList } from './level-list';

const langs = {
  en: {
    games: { 'idiom-crossword': { title: 'Idiom Crossword' } },
    'idiom-crossword': {
      levels: {
        subtitle: 'sub',
        number: 'Level {{number}}',
        difficulty: 'Tier {{value}}',
        'best-time': 'Best {{time}}',
        unplayed: 'Not played',
        locked: 'Locked',
        'stars-label': '{{count}} of 3 stars',
      },
      errors: { 'load-levels': "Couldn't load the levels." },
      actions: { retry: 'Retry' },
    },
  },
};

function level(over: Partial<PuzzleLevelSummary> = {}): PuzzleLevelSummary {
  return {
    levelIndex: 0,
    difficulty: 1,
    unlocked: true,
    bestStars: null,
    bestDurationMs: null,
    ...over,
  };
}

function render(levels: readonly PuzzleLevelSummary[] | 'error') {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      LevelList,
      TranslocoTestingModule.forRoot({
        langs,
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      provideRouter([]),
      {
        provide: PuzzleApiService,
        useValue: {
          listLevels: vi.fn(() =>
            levels === 'error' ? throwError(() => new Error('boom')) : of(levels),
          ),
        },
      },
    ],
  });

  const fixture = TestBed.createComponent(LevelList);
  fixture.detectChanges();
  return fixture;
}

function cards(fixture: ReturnType<typeof render>): HTMLLIElement[] {
  return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('li'));
}

describe('LevelList', () => {
  it('renders one card per level', () => {
    const fixture = render([level(), level({ levelIndex: 1, unlocked: false })]);

    expect(cards(fixture)).toHaveLength(2);
  });

  it('renders an unlocked level as a link to its play route', () => {
    const fixture = render([level({ levelIndex: 2 })]);

    const link = cards(fixture)[0].querySelector('a');
    expect(link).not.toBeNull();
    expect(link!.getAttribute('href')).toBe('/g/idiom-crossword/levels/2');
  });

  it('renders a locked level as inert, with no link and aria-disabled', () => {
    const fixture = render([level({ unlocked: false })]);

    const card = cards(fixture)[0];
    expect(card.querySelector('a')).toBeNull();
    expect(card.querySelector('button')).toBeNull();
    expect(card.querySelector('[aria-disabled="true"]')).not.toBeNull();
    // Lock state carried by text, not colour alone.
    expect(card.textContent).toContain('Locked');
  });

  it('shows earned stars and the best time for a completed level', () => {
    const fixture = render([level({ bestStars: 2, bestDurationMs: 95_000 })]);

    const card = cards(fixture)[0];
    // 1:35
    expect(card.textContent).toContain('1:35');
    expect(card.textContent).toContain('2 of 3 stars');
  });

  it('marks an unplayed level as such', () => {
    const fixture = render([level()]);

    expect(cards(fixture)[0].textContent).toContain('Not played');
  });

  it('offers a retry when the fetch fails', () => {
    const fixture = render('error');

    const host = fixture.nativeElement as HTMLElement;
    expect(host.textContent).toContain("Couldn't load the levels.");
    expect(host.querySelector('button')?.textContent).toContain('Retry');
  });
});
