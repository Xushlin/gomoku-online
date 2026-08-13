import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { GameCatalogService } from '../../games/game-catalog.service';
import type { GameManifest } from '../../games/game-manifest';
import { LanguageService } from '../../core/i18n/language.service';

/**
 * Platform game catalogue. Renders one card per registry entry, so shipping a
 * game never touches this component.
 *
 * There are no loading / empty / error states by design: the registry is a
 * static import, so there is nothing to fetch, fail, or come back empty.
 */
@Component({
  selector: 'app-catalog',
  standalone: true,
  imports: [RouterLink, TranslocoPipe],
  templateUrl: './catalog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Catalog {
  private readonly catalog = inject(GameCatalogService);
  private readonly language = inject(LanguageService);

  protected readonly games = computed(() => this.catalog.all());

  /** `catalog.category-match` / `-puzzle` / `-score`. */
  protected categoryKey(game: GameManifest): string {
    return `catalog.category-${game.category}`;
  }

  /**
   * True when the game's content does not exist in the locale the user is
   * reading the UI in — the idiom games are Chinese-content whatever the
   * chrome says. Drives the advisory badge; it does not block launching.
   */
  protected showsContentWarning(game: GameManifest): boolean {
    return !game.contentLocales.includes(this.language.current());
  }
}
