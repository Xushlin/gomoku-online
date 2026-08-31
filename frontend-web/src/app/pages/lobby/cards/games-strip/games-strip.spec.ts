import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { describe, expect, it } from 'vitest';
import { GAME_REGISTRY } from '../../../../games';
import { GamesStrip } from './games-strip';

function mount() {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      GamesStrip,
      TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
      }),
    ],
    providers: [provideRouter([])],
  });
  const fixture = TestBed.createComponent(GamesStrip);
  fixture.detectChanges();
  return fixture;
}

describe('GamesStrip', () => {
  it('lists exactly the playable games', () => {
    const expected = GAME_REGISTRY.filter((g) => g.status === 'available' && !!g.launchRoute);
    const links = mount().nativeElement.querySelectorAll('a[data-game-key]');

    expect(links.length).toBe(expected.length);
    expect(expected.length).toBeGreaterThan(0);
  });

  it('omits planned games — and there are none left to omit', () => {
    const planned = GAME_REGISTRY.filter((g) => g.status === 'planned');
    const el = mount().nativeElement as HTMLElement;

    // 猜成语 was the **last** planned game, so this set is now empty and the walk
    // below asserts nothing. That is stated rather than hidden.
    //
    // The guard here used to be `expect(planned.length).toBeGreaterThan(0)` — an
    // anti-vacuity check, and it did its job: shipping the tenth game turned this
    // walk into an empty loop and the suite went red instead of silently passing.
    // Replacing it with `toEqual([])` keeps a live assertion in its place: it goes
    // red the day an eleventh game is declared `planned`, which is exactly when
    // this walk becomes real again.
    expect(planned.map((g) => g.key)).toEqual([]);

    for (const game of planned) {
      expect(el.querySelector(`a[data-game-key="${game.key}"]`)).toBeNull();
    }
  });

  it('links each game at its own launch route', () => {
    const el = mount().nativeElement as HTMLElement;

    for (const game of GAME_REGISTRY.filter((g) => g.status === 'available' && g.launchRoute)) {
      const link = el.querySelector(`a[data-game-key="${game.key}"]`);
      expect(link, `${game.key} has no link`).not.toBeNull();
      expect(link!.getAttribute('href')).toBe(game.launchRoute);
    }
  });

  it('reads the registry, so a new game needs no edit here', () => {
    // The assertion is structural: the component's only data source is the
    // registry, so the count tracks it. If someone hardcodes a list, the first
    // test above starts failing the moment the registry and the list disagree.
    const source = GamesStrip.toString();

    expect(source).not.toMatch(/gomoku|xiangqi|klotski/);
  });
});
