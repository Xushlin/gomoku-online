import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { UserPublicProfileDto } from '../../../core/api/models/user-profile.model';
import { PresenceApiService } from '../../../core/api/presence-api.service';
import { UsersApiService } from '../../../core/api/users-api.service';
import { LanguageService } from '../../../core/i18n/language.service';
import { GameCapabilitiesService } from '../../../games/game-capabilities.service';
import { GameCatalogService } from '../../../games/game-catalog.service';
import { GamesList } from './games-list/games-list';

/**
 * What the server falls back to when a request carries no `gameKey`. Mirrored
 * here only so the switcher can show which chip is active on first paint — the
 * page still sends no parameter, so the server remains the one deciding.
 */
const DEFAULT_GAME_KEY = 'gomoku';

@Component({
  selector: 'app-profile-page',
  standalone: true,
  imports: [CommonModule, GamesList, RouterLink, TranslocoPipe],
  templateUrl: './profile-page.html',
  styles: [':host { display: block; width: 100%; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfilePage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly users = inject(UsersApiService);
  private readonly presenceApi = inject(PresenceApiService);
  private readonly capabilities = inject(GameCapabilitiesService);
  private readonly catalog = inject(GameCatalogService);
  protected readonly language = inject(LanguageService);

  protected userId = signal<string | null>(null);
  protected readonly profile = signal<UserPublicProfileDto | null>(null);
  protected readonly loading = signal<boolean>(true);
  protected readonly notFound = signal<boolean>(false);
  protected readonly loadError = signal<boolean>(false);
  /** Presence state. `null` = unknown / failed → don't render the dot. */
  protected readonly presence = signal<boolean | null>(null);

  /**
   * Which game's record is on screen. `null` = the first paint, where we
   * deliberately send no `gameKey` and let the server's gomoku default answer —
   * that is what already-published clients see, so it is what the profile page
   * shows before anyone touches the switcher.
   */
  protected readonly selectedGameKey = signal<string | null>(null);

  /** Rated games only — an unrated game has no record worth switching to. */
  protected readonly ratedGames = computed(() =>
    this.capabilities.ratedKeys().map((key) => ({
      key,
      titleKey: this.catalog.byKey(key)?.titleKey ?? key,
    })),
  );

  /** The key the switcher shows as active. Falls back to the server's default. */
  protected readonly activeGameKey = computed(
    () => this.selectedGameKey() ?? DEFAULT_GAME_KEY,
  );

  /**
   * True when this player has never finished a game of the selected type.
   *
   * The server answers 200 + initial values (rating 1200, all counters 0)
   * rather than 404 — "this person exists but has not played this game" is a
   * normal answer, and 404 would be mis-reported as "user not found". But
   * rendering that payload as-is reads as *1200, 0-0-0*, i.e. a beginner who
   * has played, not someone who has never touched the game. When a new game
   * ships that is true of nearly every user, so it is not an edge case.
   */
  protected readonly hasNoGames = computed(() => this.profile()?.gamesPlayed === 0);

  protected readonly winRateLabel = computed<string>(() => {
    const p = this.profile();
    if (!p) return '—';
    const denom = p.wins + p.losses + p.draws;
    if (denom === 0) return '—';
    return `${((p.wins / denom) * 100).toFixed(1)}%`;
  });

  ngOnInit(): void {
    this.capabilities.ensureLoaded();
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }
    this.userId.set(id);
    this.fetch(id);
    this.fetchPresence(id);
  }

  protected retry(): void {
    const id = this.userId();
    if (id) this.fetch(id, this.selectedGameKey() ?? undefined);
  }

  /** Switch which game's record the header shows; re-fetches with `?gameKey=`. */
  protected selectGame(gameKey: string): void {
    if (gameKey === this.activeGameKey()) return;
    this.selectedGameKey.set(gameKey);
    const id = this.userId();
    if (id) this.fetch(id, gameKey);
  }

  private fetch(id: string, gameKey?: string): void {
    this.loading.set(true);
    this.notFound.set(false);
    this.loadError.set(false);
    // Blank the old game's numbers while the new ones are in flight — leaving
    // them up would show one game's record under another game's label.
    this.profile.set(null);
    this.users.getProfile(id, gameKey).subscribe({
      next: (p) => {
        this.profile.set(p);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        if (err instanceof HttpErrorResponse && err.status === 404) {
          this.notFound.set(true);
          return;
        }
        this.loadError.set(true);
      },
    });
  }

  /**
   * Presence is a property of the person, not of the game being viewed, so it
   * is fetched once rather than on every switch.
   */
  private fetchPresence(id: string): void {
    this.presence.set(null);
    this.presenceApi.getUserOnline(id).subscribe({
      next: (online) => this.presence.set(online),
      error: () => this.presence.set(null),
    });
  }
}
