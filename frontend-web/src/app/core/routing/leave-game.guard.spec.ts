import { Dialog } from '@angular/cdk/dialog';
import { TestBed } from '@angular/core/testing';
import type { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { firstValueFrom, of, type Observable } from 'rxjs';
import { beforeEach, describe, expect, it } from 'vitest';
import { leaveGameGuard, type ConfirmsLeaving } from './leave-game.guard';

/** 记下每一次 `Dialog.open`,并按 `answer` 回话。 */
const opened: { component: unknown; data: unknown }[] = [];
let answer: boolean | undefined = true;

function setup() {
  opened.length = 0;
  answer = true;
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      {
        provide: Dialog,
        useValue: {
          open: (component: unknown, config?: { data?: unknown }) => {
            opened.push({ component, data: config?.data });
            return { closed: of(answer) };
          },
        },
      },
    ],
  });
}

const nextState = (url: string) => ({ url }) as RouterStateSnapshot;
const route = {} as ActivatedRouteSnapshot;

/** 守卫可能同步返回 boolean,也可能返回 Observable —— 两种都要能问出答案。 */
async function run(component: unknown, url = '/home'): Promise<boolean> {
  const result = TestBed.runInInjectionContext(() =>
    leaveGameGuard(component, route, nextState('/rooms/r-1'), nextState(url)),
  );
  return typeof result === 'boolean' ? result : firstValueFrom(result as Observable<boolean>);
}

describe('leaveGameGuard', () => {
  beforeEach(() => setup());

  it('lets a page with no opinion through', async () => {
    expect(await run({})).toBe(true);
    expect(opened).toHaveLength(0);
  });

  it('lets a page through when it says leaving is free', async () => {
    const page: ConfirmsLeaving = { leaveWarningKey: () => null };
    expect(await run(page)).toBe(true);
    expect(opened).toHaveLength(0);
  });

  it('asks, with the page own message, when leaving costs something', async () => {
    const page: ConfirmsLeaving = { leaveWarningKey: () => 'game.leave-confirm.tetris' };
    expect(await run(page)).toBe(true);
    expect(opened).toHaveLength(1);
    expect(opened[0].data).toEqual({ messageKey: 'game.leave-confirm.tetris' });
  });

  it('blocks the navigation when the answer is stay', async () => {
    // 两头都要有样本。只断言「确认后放行」的话,一个永远 return true 的守卫同样是绿的
    // —— 而那正是这个功能什么都没做的样子。
    answer = false;
    const page: ConfirmsLeaving = { leaveWarningKey: () => 'game.leave-confirm.match' };
    expect(await run(page)).toBe(false);
    expect(opened).toHaveLength(1);
  });

  it('treats a dismissed dialog (ESC / backdrop) as stay', async () => {
    answer = undefined;
    const page: ConfirmsLeaving = { leaveWarningKey: () => 'game.leave-confirm.match' };
    expect(await run(page)).toBe(false);
  });

  it('never asks on the way to /login', async () => {
    // 401 之后是拦截器发起跳转,组件拦不住;而会话已经过期时问「要离开吗」,玩家点
    // 「留下」就留在一个连不上服务端的页面上。
    const page: ConfirmsLeaving = { leaveWarningKey: () => 'game.leave-confirm.match' };
    expect(await run(page, '/login')).toBe(true);
    expect(opened).toHaveLength(0);
  });

  it('still asks on the way to a route whose name merely starts like another', async () => {
    // 前一条用的是 startsWith,所以这条钉住它没有把 /logout-ish 的东西一起放走。
    const page: ConfirmsLeaving = { leaveWarningKey: () => 'game.leave-confirm.match' };
    expect(await run(page, '/leaderboard')).toBe(true);
    expect(opened).toHaveLength(1);
  });
});
