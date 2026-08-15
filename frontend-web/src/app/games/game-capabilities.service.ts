import { computed, inject, Injectable, signal } from '@angular/core';
import { GamesApiService } from '../core/api/games-api.service';
import type { GameDescriptor } from '../core/api/models/game-descriptor.model';

/**
 * Server-declared capabilities, keyed by game key, loaded once per session.
 *
 * Deliberately a **separate service from {@link GameCatalogService}** rather
 * than merged into it. The catalogue is a static import — synchronous, never
 * fails, never empty — and several components and specs depend on that. Making
 * it async to fold in one HTTP call would push loading/error states into every
 * one of them for the sake of two booleans.
 *
 * So the two layers stay separate and compose at the call site: the manifest
 * says *what games exist and how to reach them*, this says *what the server
 * lets them do*. A key with no descriptor means "not applicable", not "false" —
 * puzzle games have no `IGameRules` at all, and collapsing that into
 * `isRated: false` would make "tic-tac-toe is unrated" indistinguishable from
 * "idiom crossword isn't a versus game".
 */
export abstract class GameCapabilitiesService {
  /** Kick off the one-time load. Safe to call repeatedly; only the first fetches. */
  abstract ensureLoaded(): void;

  /**
   * Capabilities for one game, or `undefined` when the server has no
   * `IGameRules` for it — puzzle games and not-yet-implemented ones.
   * Also `undefined` before the load resolves.
   */
  abstract of(gameKey: string): GameDescriptor | undefined;

  /** Keys of every rated game, ascending. Empty before the load resolves. */
  abstract ratedKeys(): readonly string[];

  /** True once the request has settled, either way. Drives loading states. */
  abstract loaded(): boolean;
}

@Injectable()
export class DefaultGameCapabilitiesService extends GameCapabilitiesService {
  private readonly api = inject(GamesApiService);

  private readonly descriptors = signal<readonly GameDescriptor[]>([]);
  private readonly settled = signal(false);
  private started = false;

  private readonly byKey = computed(
    () => new Map(this.descriptors().map((d) => [d.gameKey, d])),
  );

  private readonly rated = computed(() =>
    this.descriptors()
      .filter((d) => d.isRated)
      .map((d) => d.gameKey),
  );

  ensureLoaded(): void {
    if (this.started) return;
    this.started = true;
    this.api.list().subscribe({
      next: (items) => {
        this.descriptors.set(items);
        this.settled.set(true);
      },
      // A failed load leaves every game "not applicable", which degrades to
      // "no ladder links, no game switcher" — the pre-change UI. That is the
      // right way to fail: a missing affordance, never a wrong one.
      error: () => this.settled.set(true),
    });
  }

  of(gameKey: string): GameDescriptor | undefined {
    return this.byKey().get(gameKey);
  }

  ratedKeys(): readonly string[] {
    return this.rated();
  }

  loaded(): boolean {
    return this.settled();
  }
}
