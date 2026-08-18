import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { describe, expect, it } from 'vitest';
import { GameCapabilitiesService } from '../../games/game-capabilities.service';
import { StubGameCapabilities } from '../../games/game-capabilities.stub';
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

const AVAILABLE_PUZZLE: GameManifest = {
  key: 'idiom-crossword',
  category: 'puzzle',
  status: 'available',
  titleKey: 'games.idiom-crossword.title',
  descriptionKey: 'games.idiom-crossword.description',
  icon: '田',
  contentLocales: ['zh-CN'],
  launchRoute: '/g/idiom-crossword',
};

const AVAILABLE_UNRATED: GameManifest = {
  key: 'tictactoe',
  category: 'match',
  status: 'available',
  titleKey: 'games.tictactoe.title',
  descriptionKey: 'games.tictactoe.description',
  icon: '井',
  contentLocales: ['zh-CN', 'en'],
  launchRoute: '/g/tictactoe',
};

const AVAILABLE_SCORE: GameManifest = {
  key: 'tetris',
  category: 'score',
  status: 'available',
  titleKey: 'games.tetris.title',
  descriptionKey: 'games.tetris.description',
  icon: '块',
  contentLocales: ['zh-CN', 'en'],
  launchRoute: '/g/tetris',
};

const PLANNED_SCORE: GameManifest = { ...AVAILABLE_SCORE, status: 'planned', launchRoute: undefined };

const langs = {
  en: {
    catalog: {
      title: 'Games',
      subtitle: 'sub',
      'coming-soon': 'Coming soon',
      'scores-link': 'High scores',
      'chinese-only': 'Chinese content',
      'category-match': 'Versus',
      'category-puzzle': 'Puzzle',
      'category-score': 'Score attack',
      'leaderboard-link': 'Leaderboard',
    },
    games: {
      gomoku: { title: 'Gomoku', description: 'five in a row' },
      'idiom-crossword': { title: 'Idiom Crossword', description: 'grid of idioms' },
      tictactoe: { title: 'Tic-tac-toe', description: 'three in a row' },
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
      'leaderboard-link': '排行榜',
    },
    games: {
      gomoku: { title: '五子棋', description: '连五' },
      'idiom-crossword': { title: '成语纵横', description: '成语方格' },
      tictactoe: { title: '一字棋', description: '三连' },
    },
  },
};

function renderCatalog(
  games: readonly GameManifest[],
  locale: 'en' | 'zh-CN',
  capabilities: StubGameCapabilities = new StubGameCapabilities(),
) {
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
      { provide: GameCapabilitiesService, useValue: capabilities },
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

  it('adds a leaderboard link only when the server says the game is rated', () => {
    const fixture = renderCatalog([AVAILABLE], 'en', StubGameCapabilities.rated('gomoku'));
    const ladder = cards(fixture)[0].querySelector('a[href="/g/gomoku/leaderboard"]');
    expect(ladder).not.toBeNull();
    expect(ladder!.textContent).toContain('Leaderboard');
  });

  it('gives an unrated game NO leaderboard link', () => {
    // This is the executable form of the argument for reading `isRated` from
    // the server instead of copying it into the manifest. A stale copy would
    // link to a permanently empty ladder — which looks exactly like a new game
    // nobody has played, i.e. the mismatch would never be noticed. If this
    // test ever gets deleted, that copy has crept back in.
    const fixture = renderCatalog(
      [AVAILABLE_UNRATED],
      'en',
      new StubGameCapabilities([
        { gameKey: 'tictactoe', isRated: false, supportsHumanVsHuman: false, supportsAi: true, rows: 3, cols: 3 },
      ]),
    );
    expect(cards(fixture)[0].querySelector('a[href$="/leaderboard"]')).toBeNull();
  });

  it('gives a puzzle game NO leaderboard link', () => {
    // Puzzle games have no IGameRules, so no capability at all — "not
    // applicable" rather than `isRated: false`. Their ladder is stars + time.
    const fixture = renderCatalog([AVAILABLE_PUZZLE], 'en', StubGameCapabilities.rated('gomoku'));
    expect(cards(fixture)[0].querySelector('a[href$="/leaderboard"]')).toBeNull();
  });

  it('gives a planned game NO leaderboard link even if the key is rated', () => {
    const fixture = renderCatalog([PLANNED_ZH_ONLY], 'en', StubGameCapabilities.rated('idiom-crossword'));
    expect(cards(fixture)[0].querySelector('a[href$="/leaderboard"]')).toBeNull();
  });

  it('adds a high-scores link to an available score-attack game', () => {
    // Gated on `category`, not on a server flag — and that is not a relapse into
    // client-side copies. `isRated` is a server judgement whose stale copy points at
    // a permanently empty ladder; `category` is declared here, already drives the
    // grouping, and there is no server flag to read at all (tetris has no
    // `IGameRules`, so `GET /api/games` never describes it).
    const fixture = renderCatalog([AVAILABLE_SCORE], 'en');
    const link = cards(fixture)[0].querySelector('a[href="/g/tetris/scores"]');

    expect(link).not.toBeNull();
    expect(link!.textContent).toContain('High scores');
  });

  it('gives a match game NO high-scores link', () => {
    const fixture = renderCatalog([AVAILABLE], 'en', StubGameCapabilities.rated('gomoku'));

    expect(cards(fixture)[0].querySelector('a[href$="/scores"]')).toBeNull();
  });

  it('gives a puzzle game NO high-scores link', () => {
    const fixture = renderCatalog([AVAILABLE_PUZZLE], 'en');

    expect(cards(fixture)[0].querySelector('a[href$="/scores"]')).toBeNull();
  });

  it('gives a planned score game NO high-scores link', () => {
    const fixture = renderCatalog([PLANNED_SCORE], 'en');

    expect(cards(fixture)[0].querySelector('a[href$="/scores"]')).toBeNull();
  });

  it('renders no leaderboard links at all before capabilities load', () => {
    // Degrading to a missing affordance is right; degrading to a wrong one
    // (a link onto an empty ladder) would not be.
    const fixture = renderCatalog([AVAILABLE], 'en', new StubGameCapabilities());
    expect(cards(fixture)[0].querySelector('a[href$="/leaderboard"]')).toBeNull();
  });

  it('the launch link still covers the whole card', () => {
    // The card is no longer one big <a> — nesting the ladder link inside it
    // would be invalid HTML. The launch link stretches via a pseudo-element
    // instead, so clicking anywhere but the ladder link still launches.
    const fixture = renderCatalog([AVAILABLE], 'en', StubGameCapabilities.rated('gomoku'));
    const launch = cards(fixture)[0].querySelector('a[href="/home"]');
    expect(launch).not.toBeNull();
    expect(launch!.className).toContain('after:inset-0');
  });

  it('shows the category badge', () => {
    const fixture = renderCatalog([AVAILABLE, PLANNED_ZH_ONLY], 'en');
    const [match, puzzle] = cards(fixture);
    expect(match.textContent).toContain('Versus');
    expect(puzzle.textContent).toContain('Puzzle');
  });
});
