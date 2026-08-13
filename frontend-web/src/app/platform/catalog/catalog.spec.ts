import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { describe, expect, it } from 'vitest';
import { GameCatalogService } from '../../games/game-catalog.service';
import type { GameManifest } from '../../games/game-manifest';
import { LanguageService } from '../../core/i18n/language.service';
import { Catalog } from './catalog';

const AVAILABLE: GameManifest = {
  key: 'gomoku',
  category: 'match',
  status: 'available',
  titleKey: 'games.gomoku.title',
  descriptionKey: 'games.gomoku.description',
  icon: '⬤',
  contentLocales: ['zh-CN', 'en'],
  launchRoute: '/home',
};

const PLANNED_ZH_ONLY: GameManifest = {
  key: 'idiom-crossword',
  category: 'puzzle',
  status: 'planned',
  titleKey: 'games.idiom-crossword.title',
  descriptionKey: 'games.idiom-crossword.description',
  icon: '田',
  contentLocales: ['zh-CN'],
};

const langs = {
  en: {
    catalog: {
      title: 'Games',
      subtitle: 'sub',
      'coming-soon': 'Coming soon',
      'chinese-only': 'Chinese content',
      'category-match': 'Versus',
      'category-puzzle': 'Puzzle',
      'category-score': 'Score attack',
    },
    games: {
      gomoku: { title: 'Gomoku', description: 'five in a row' },
      'idiom-crossword': { title: 'Idiom Crossword', description: 'grid of idioms' },
    },
  },
  'zh-CN': {
    catalog: {
      title: '游戏',
      subtitle: 'sub',
      'coming-soon': '即将上线',
      'chinese-only': '中文内容',
      'category-match': '对战',
      'category-puzzle': '闯关',
      'category-score': '计分',
    },
    games: {
      gomoku: { title: '五子棋', description: '连五' },
      'idiom-crossword': { title: '成语纵横', description: '成语方格' },
    },
  },
};

function renderCatalog(games: readonly GameManifest[], locale: 'en' | 'zh-CN') {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      Catalog,
      TranslocoTestingModule.forRoot({
        langs,
        translocoConfig: { availableLangs: ['en', 'zh-CN'], defaultLang: locale },
        preloadLangs: true,
      }),
    ],
    providers: [
      provideRouter([]),
      {
        provide: GameCatalogService,
        useValue: {
          all: () => games,
          available: () => games.filter((g) => g.status === 'available'),
          planned: () => games.filter((g) => g.status === 'planned'),
          byKey: (k: string) => games.find((g) => g.key === k),
        },
      },
      { provide: LanguageService, useValue: { current: () => locale, use: () => undefined } },
    ],
  });

  const fixture = TestBed.createComponent(Catalog);
  fixture.detectChanges();
  return fixture;
}

function cards(fixture: ReturnType<typeof renderCatalog>): HTMLLIElement[] {
  return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('li'));
}

describe('Catalog', () => {
  it('renders one card per manifest', () => {
    const fixture = renderCatalog([AVAILABLE, PLANNED_ZH_ONLY], 'zh-CN');
    expect(cards(fixture).length).toBe(2);
  });

  it('renders an available game as a link to its launchRoute', () => {
    const fixture = renderCatalog([AVAILABLE], 'zh-CN');
    const link = cards(fixture)[0].querySelector('a');
    expect(link).not.toBeNull();
    expect(link!.getAttribute('href')).toBe('/home');
    expect(link!.textContent).toContain('五子棋');
  });

  it('renders a planned game as an inert, aria-disabled element with no link', () => {
    const fixture = renderCatalog([PLANNED_ZH_ONLY], 'zh-CN');
    const card = cards(fixture)[0];
    expect(card.querySelector('a')).toBeNull();
    expect(card.querySelector('button')).toBeNull();
    expect(card.querySelector('[aria-disabled="true"]')).not.toBeNull();
    expect(card.textContent).toContain('即将上线');
  });

  it('badges a Chinese-content game when the UI locale is en', () => {
    const fixture = renderCatalog([PLANNED_ZH_ONLY], 'en');
    expect(cards(fixture)[0].textContent).toContain('Chinese content');
  });

  it('does not badge a Chinese-content game when the UI locale is zh-CN', () => {
    const fixture = renderCatalog([PLANNED_ZH_ONLY], 'zh-CN');
    expect(cards(fixture)[0].textContent).not.toContain('中文内容');
  });

  it('shows the category badge', () => {
    const fixture = renderCatalog([AVAILABLE, PLANNED_ZH_ONLY], 'en');
    const [match, puzzle] = cards(fixture);
    expect(match.textContent).toContain('Versus');
    expect(puzzle.textContent).toContain('Puzzle');
  });
});
