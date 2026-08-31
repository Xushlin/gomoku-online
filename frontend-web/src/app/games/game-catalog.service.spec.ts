import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { DefaultGameCatalogService, GameCatalogService } from './game-catalog.service';

function createService(): GameCatalogService {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [{ provide: GameCatalogService, useClass: DefaultGameCatalogService }],
  });
  return TestBed.inject(GameCatalogService);
}

describe('DefaultGameCatalogService', () => {
  let svc: GameCatalogService;

  beforeEach(() => {
    svc = createService();
  });

  it('orders every available game before every planned one', () => {
    const statuses = svc.all().map((g) => g.status);
    const lastAvailable = statuses.lastIndexOf('available');
    const firstPlanned = statuses.indexOf('planned');

    expect(lastAvailable).toBeGreaterThanOrEqual(0);

    // 猜成语 shipped and it was the last planned game, so there is currently no
    // `planned` entry to order after anything. The ordering rule still holds — it
    // is just unexercised, and saying so beats a comparison against `indexOf`'s
    // -1, which is how this assertion first failed.
    if (firstPlanned === -1) {
      expect(statuses.every((s) => s === 'available')).toBe(true);
      return;
    }

    expect(firstPlanned).toBeGreaterThan(lastAvailable);
  });

  it('partitions all() into available() and planned()', () => {
    expect(svc.available().length + svc.planned().length).toBe(svc.all().length);
    expect(svc.available().every((g) => g.status === 'available')).toBe(true);
    expect(svc.planned().every((g) => g.status === 'planned')).toBe(true);
  });

  it('finds gomoku by key', () => {
    const gomoku = svc.byKey('gomoku');
    expect(gomoku?.status).toBe('available');
    expect(gomoku?.category).toBe('match');
  });

  it('returns undefined for an unknown key', () => {
    expect(svc.byKey('no-such-game')).toBeUndefined();
  });
});
