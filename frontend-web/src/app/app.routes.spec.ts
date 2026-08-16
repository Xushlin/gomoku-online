import { describe, expect, it } from 'vitest';
import { routes } from './app.routes';
import { authGuard } from './core/auth/auth.guards';
import { GAME_REGISTRY } from './games/index';

/**
 * Route-table invariants.
 *
 * Every game entry point is lazy and guarded. Both properties are easy to get right
 * by copying the entry above and easy to lose by writing a fresh one, and neither
 * failure is visible in a component test: a `component:` reference still works, it
 * just drags the game into the initial bundle for players who never open it.
 */
describe('app routes', () => {
  const gameRoutes = routes.filter((r) => r.path?.startsWith('g/'));

  it('has one entry route per game that declares a launch route', () => {
    const declared = GAME_REGISTRY.filter((g) => g.launchRoute).map((g) =>
      g.launchRoute!.replace(/^\//, ''),
    );

    for (const path of declared) {
      expect(
        routes.some((r) => r.path === path),
        `${path} is declared by a manifest but has no route`,
      ).toBe(true);
    }
  });

  it('lazy-loads every game entry route', () => {
    expect(gameRoutes.length).toBeGreaterThanOrEqual(3);
    for (const route of gameRoutes) {
      expect(route.loadComponent ?? route.loadChildren, `${route.path} is not lazy`).toBeDefined();
      expect(route.component, `${route.path} must not be eager`).toBeUndefined();
    }
  });

  it('guards every game entry route', () => {
    for (const route of gameRoutes) {
      expect(route.canMatch, `${route.path} is unguarded`).toContain(authGuard);
    }
  });

  it('routes 中国象棋 at /g/xiangqi', () => {
    const xiangqi = routes.find((r) => r.path === 'g/xiangqi');

    expect(xiangqi).toBeDefined();
    expect(xiangqi!.loadComponent).toBeTypeOf('function');
    expect(xiangqi!.canMatch).toContain(authGuard);
  });
});
