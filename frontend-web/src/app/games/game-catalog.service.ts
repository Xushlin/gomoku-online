import { Injectable } from '@angular/core';
import type { GameManifest } from './game-manifest';
import { GAME_REGISTRY } from './index';

/**
 * Abstract DI token for the game catalogue. Consumers MUST `inject(GameCatalogService)`
 * rather than importing {@link GAME_REGISTRY} directly, so specs can supply a
 * small stub instead of asserting against the whole real registry — otherwise
 * every catalogue test would need editing each time a game ships.
 */
export abstract class GameCatalogService {
  /** Every registered game, `available` entries before `planned` ones. */
  abstract all(): readonly GameManifest[];

  /** Games playable today. */
  abstract available(): readonly GameManifest[];

  /** Games announced but not yet built. */
  abstract planned(): readonly GameManifest[];

  /** Look up one manifest, or `undefined` when the key is unknown. */
  abstract byKey(key: string): GameManifest | undefined;
}

/** Default implementation, reading the static {@link GAME_REGISTRY}. */
@Injectable()
export class DefaultGameCatalogService extends GameCatalogService {
  private readonly ordered: readonly GameManifest[] = [
    ...GAME_REGISTRY.filter((g) => g.status === 'available'),
    ...GAME_REGISTRY.filter((g) => g.status === 'planned'),
  ];

  all(): readonly GameManifest[] {
    return this.ordered;
  }

  available(): readonly GameManifest[] {
    return this.ordered.filter((g) => g.status === 'available');
  }

  planned(): readonly GameManifest[] {
    return this.ordered.filter((g) => g.status === 'planned');
  }

  byKey(key: string): GameManifest | undefined {
    return this.ordered.find((g) => g.key === key);
  }
}
