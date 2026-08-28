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

  /**
   * 按**房间的**棋种键解析清单 —— 匹配清单自己的 `key`,**或**它声明的任一
   * `companionRoomKeys`。
   *
   * 它与 {@link byKey} 不能合成一个,而两个方向都要紧:
   *
   * - `byKey('xiangqi-endgame')` MUST 是 `undefined` —— 残局没有自己的清单,它不该
   *   出现在目录页上,也不该有自己的大厅;
   * - `byRoomKey('xiangqi-endgame')` MUST 给出**象棋**那份 —— 一间残局房要画象棋的
   *   纹章、用象棋的席位名。
   *
   * 它同时收拢一处已经存在的重复:大厅的房间行此前自己拼了一张「伴生键 → 主棋种」的表。
   * 一份被复制的解析规则迟早与另一份不一致,而这个仓库为这个形状付过很多次账。
   */
  abstract byRoomKey(key: string): GameManifest | undefined;
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

  /** @inheritdoc */
  byRoomKey(key: string): GameManifest | undefined {
    return this.ordered.find(
      (g) => g.key === key || (g.companionRoomKeys ?? []).includes(key),
    );
  }
}
