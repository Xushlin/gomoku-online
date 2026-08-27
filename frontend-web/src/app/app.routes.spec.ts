import { describe, expect, it } from 'vitest';
import { routes } from './app.routes';
import { authGuard } from './core/auth/auth.guards';
import { leaveGameGuard } from './core/routing/leave-game.guard';
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

  /**
   * Does a concrete URL path match a route pattern? `:param` segments match any
   * single segment.
   *
   * Needed because `generalize-lobby` gave gomoku the launch route
   * `/g/gomoku/lobby`, served by the parameterised `g/:gameKey/lobby`. Comparing
   * strings would have failed for a route that is perfectly well declared — and,
   * worse, would have pushed the next game towards its own literal route just to
   * keep this test quiet.
   */
  const matches = (pattern: string, path: string): boolean => {
    const p = pattern.split('/');
    const s = path.split('/');
    return p.length === s.length && p.every((seg, i) => seg.startsWith(':') || seg === s[i]);
  };

  it('has one entry route per game that declares a launch route', () => {
    const declared = GAME_REGISTRY.filter((g) => g.launchRoute).map((g) =>
      g.launchRoute!.replace(/^\//, ''),
    );

    for (const path of declared) {
      expect(
        routes.some((r) => r.path !== undefined && matches(r.path, path)),
        `${path} is declared by a manifest but has no route`,
      ).toBe(true);
    }
  });

  it('no available game launches at /home — that is the platform home', () => {
    // `/home` belongs to no game. Gomoku pointed there while it *was* gomoku's
    // lobby; a manifest still pointing there after generalize-lobby would send
    // players from the catalogue to a page with no trace of the game they picked.
    for (const game of GAME_REGISTRY.filter((g) => g.status === 'available')) {
      expect(game.launchRoute, `${game.key} still launches at /home`).not.toBe('/home');
    }
  });

  it('lazy-loads every game entry route', () => {
    expect(gameRoutes.length).toBeGreaterThanOrEqual(3);
    for (const route of gameRoutes) {
      expect(route.loadComponent ?? route.loadChildren, `${route.path} is not lazy`).toBeDefined();
      expect(route.component, `${route.path} must not be eager`).toBeUndefined();
    }
  });

  /*
   * **这条原来是恒真的,而修法与隔壁那条一模一样。**
   *
   * 原文是逐条 `expect(route.canMatch).toContain(authGuard)` —— 而 `canMatch` 是
   * `undefined` 时它**不失败**。同一个坑这个文件下面那条走查已经记过一次(那次是
   * `canDeactivate`),这次是加古谱那两条无守卫路由时撞出来的:两条 `canMatch=undefined`
   * 就摆在 `gameRoutes` 里,而这条测试是绿的。
   *
   * 所以改成**数出来再比空列表**;而「故意匿名」由路由上的 `data.publicContent` 显式标记 ——
   * 一处写下来的决定,而不是一个洞。豁免名单断言的是**恰好**这些,所以下一条匿名路由
   * 必须有人来改这里,那正是该有人看一眼的时刻。
   */
  it('guards every game entry route except the ones marked public', () => {
    const isPublic = (r: (typeof gameRoutes)[number]) => r.data?.['publicContent'] === true;
    const unguarded = gameRoutes.filter((r) => !r.canMatch?.includes(authGuard) && !isPublic(r));
    expect(unguarded.map((r) => r.path ?? '(no path)')).toEqual([]);

    // 两边都要有样本,否则上面那行会在「全都匿名」或「全都有守卫」时空转。
    // 三层古谱:谱的清单 -> 单谱目录 -> 学习页。**「恰好」而不是「至少」** —— 加第四条
    // 匿名路由必须有人改这里,而 `add-xiangqi-endgames` 加第三条时它确实红了一次。
    expect(gameRoutes.filter(isPublic).map((r) => r.path)).toEqual([
      'g/xiangqi/manual',
      'g/xiangqi/manual/:manualKey',
      'g/xiangqi/manual/:manualKey/:lineId',
    ]);
    expect(gameRoutes.filter((r) => r.canMatch?.includes(authGuard)).length).toBeGreaterThan(5);
  });

  it('puts the leave guard on every route, not just the game ones', () => {
    /*
     * 判据是**走一遍整张表**,不是「游戏路由都挂了」。挑几条挂的做法会在第十款游戏
     * 落地那天漏掉一条,而漏掉的表现是**没有弹框** —— 一个看不出来的缺陷。
     *
     * 今天这条不可能失败:`withLeaveGuard` 对整个数组 map。它防的是有人把某一条
     * 从那层 map 里拆出来单写。
     */
    expect(routes.length).toBeGreaterThan(10);

    /*
     * 数出来,而不是逐条 `toContain`。第一版写的是
     *   `expect(route.canDeactivate).toContain(leaveGameGuard)`
     * 而 `canDeactivate` 是 `undefined` 时**它不失败** —— 于是这条走查在它唯一
     * 存在的理由(某一条路由没挂上)下面是绿的。变异证明的。
     */
    const unguarded = routes.filter((r) => !r.canDeactivate?.includes(leaveGameGuard));
    expect(unguarded.map((r) => r.path ?? '(no path)')).toEqual([]);
  });

  it('routes 中国象棋 at /g/xiangqi', () => {
    const xiangqi = routes.find((r) => r.path === 'g/xiangqi');

    expect(xiangqi).toBeDefined();
    expect(xiangqi!.loadComponent).toBeTypeOf('function');
    expect(xiangqi!.canMatch).toContain(authGuard);
  });
});
