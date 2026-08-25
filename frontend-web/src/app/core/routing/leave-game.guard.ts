import { inject, Injector } from '@angular/core';
import type { CanDeactivateFn } from '@angular/router';
import { from, map, switchMap, type Observable } from 'rxjs';

/**
 * 一个页面对「现在离开贵不贵」的回答。
 *
 * **守卫只认这个方法,不认路由表、也不认组件清单。** 挑几条「游戏路由」挂守卫的做法
 * 会在第十款游戏落地那天漏掉一条,而漏掉的表现是**没有弹框** —— 一个看不出来的缺陷。
 * 决定权放在组件这一侧之后,加一款游戏仍然是「落一个文件」。
 *
 * 实现它是**可选的**:没有这个方法的页面(登录、大厅、排行榜)一律放行。
 */
export interface ConfirmsLeaving {
  /** 现在离开要警告什么(i18n 键);`null` = 走了不心疼。 */
  leaveWarningKey(): string | null;
}

/** 会话已经过期时跳的地方 —— 见下面为什么它必须放行。 */
const LOGIN_URL = '/login';

/**
 * 对局进行中离开先确认。挂在**每一条**路由上(见 `app.routes.ts` 的 `withLeaveGuard`)。
 *
 * **去 `/login` 的导航一律放行。** 401 之后是**拦截器**(不是组件)发起跳转,那条路径
 * 绕不过守卫;而会话已经过期时问「要离开吗」,玩家点「留下」就留在一个连不上服务端的
 * 页面上。**一个把人困住的确认框比没有确认框更糟。**
 *
 * **弹框那一整块是动态 `import()` 的,连 `Dialog` 一起。** 这个文件被路由表引用,所以
 * 它在初始包里;而 `@angular/cdk/dialog` 与那个只在点走时才用得上的组件不必跟着进去。
 * (顺带:这也让 `app.routes.spec.ts` 这种不建 TestBed 的纯单测能直接 import 本文件。)
 */
export const leaveGameGuard: CanDeactivateFn<unknown> = (
  component,
  _currentRoute,
  _currentState,
  nextState,
): boolean | Observable<boolean> => {
  if (nextState.url.startsWith(LOGIN_URL)) return true;

  const key = (component as Partial<ConfirmsLeaving> | null)?.leaveWarningKey?.() ?? null;
  if (!key) return true;

  const injector = inject(Injector);
  return from(import('./leave-confirm-dialog')).pipe(
    switchMap(({ openLeaveConfirm }) => openLeaveConfirm(injector, key)),
    map((confirmed) => confirmed === true),
  );
};
