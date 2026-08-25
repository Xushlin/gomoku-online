# tasks — add-leave-game-guard

## 1. 守卫与弹框

- [x] `core/routing/leave-game.guard.ts`:`ConfirmsLeaving` 接口 + `leaveGameGuard`。鸭子类型读可选方法,**不认路由、不认组件清单**。
- [x] `core/routing/leave-confirm-dialog.{ts,html}`:CDK Dialog + `openLeaveConfirm(injector, key)` —— 守卫和「离开房间」按钮共用它,所以配置只有一处。**没有 `window.confirm`。**
- [x] `app.routes.ts`:`withLeaveGuard(routes)` 对整个数组 map。**不是每条手写一遍** —— 挑着挂会在第十款游戏那天漏掉一条,而漏掉的表现是没有弹框。
- [x] 守卫 MUST 放行去 `/login`:401 之后是拦截器发起的,绕不过守卫。
- [x] 弹框那一整块动态 `import()`,连 `Dialog` 一起。**量过:** 弹框模板落在 `chunk-ZGXKTCHX.js`,而 `index.html` 只加载 `main-*.js` —— 不在初始包里。

## 2. 四个组件各自的判据

- [x] 房间页:`status === 'Playing'` 且 `mySeat() !== null`(**座位号,不是颜色** —— 三座位棋种里 2 号座位既不是黑也不是白)。
- [x] 华容道:`phase === 'playing' && moves().length > 0`。
- [x] 成语纵横:`solvedSlots().size > 0 && !board.complete()`。
- [x] 俄罗斯方块:`phase` ∈ {`playing`, `paused`, `submitting`}。
- [x] 四条各自的 i18n 键(`game.leave-confirm.*`,双语各 +7),文案说各自的代价。两份 JSON 的 key 集合仍然相等(489 = 489)。

## 3. 刻意离开不再问

- [x] `leaveTo(url)`:置 `exiting` 再导航。五处代码发起的导航全部改走它。
- [x] `leaveWarningKey()` 在 `exiting()` 为真时返回 `null`。
- [x] 「离开房间」按钮先弹**同一个**弹框、说**同一句**话,确认后才发请求。等待中的房间不问。
- [x] **源码走查移到 lint**(`scripts/check-source-rules.mjs`):`room-page.ts` 里 `router.navigate*` 只许在 `leaveTo` 之后。放不进 vitest —— spec 的 TS 配置没有 `node:fs` 类型。检查在**一个调用都没匹配到时也失败**。

## 4. 测试(+16)

- [x] 守卫单测 7 条:没有方法 / 返回 null → 放行;返回键 → 开框并带上那个键;**取消 → 不放行**;ESC 关掉(`undefined`)→ 当作留下;去 `/login` 不问;而去 `/leaderboard` 要问(钉住 `startsWith` 没有顺手放走别的)。
- [x] 路由走查:**数出没挂守卫的那些**,期望空列表。
- [x] 房间页:问一次且只问一次、选留下就不走、观众不问(**同一条测试里先断言玩家会被问**)、等待中的房间不问。
- [x] 华容道 / 方块 / 成语纵横:各一条,**两头都在同一条测试里**。

## 5. 变异(七条,全部先看到红)

- [x] 守卫把「留下」当「离开」→ 红。
- [x] 观众也返回警告键 → 红。
- [x] 华容道不看步数 → 红。
- [x] 一条路由溜出 `withLeaveGuard` → **第一次是绿的**,见下。修好后红,并点名 `g/tetris`。
- [x] `leaveTo` 不置 `exiting` → 红(会问第二遍)。
- [x] 按钮不等答案就走(fire-and-forget)→ 红。
- [x] 房间页绕过 `leaveTo` 直接调路由器 → **lint** 红,并点名行号。

**路由走查第一版是绿的,而它是唯一为「有人漏挂一条」存在的断言。** 原来写的是逐条
`expect(route.canDeactivate).toContain(leaveGameGuard)`,而 `canDeactivate` 是 `undefined`
时**它不失败**。改成数出不合格的那些再比空列表 —— 一个长度比较没法恒真。**是变异发现的,
不是我读出来的。**

另外两条一开始不是变异:删掉按钮那段调用会让 `openLeaveConfirm` 变成未使用的 import
(编译错,读起来和 kill 一样);还有一条模式写错了缩进,`count == 0` 直接跳过。

## 6. 量出来的东西

- [x] **浏览器里真的拦住了**(4205 + 5245,自己的端口):一局 `status: Playing` 的五子棋里点 header 的 Games →
      **URL 不动**、CDK 弹框出现、正文是房间页那一句;点「留下」→ 弹框消失、仍在房间、棋盘还在;
      再点 Games → 点「离开」→ 到了 `/games`。
- [x] 前两次都白试了,而原因值得记:**回合超时 60 秒,我做完几个来回之后对局已经 `Finished`**,
      于是判据正确地返回 `null`、放行。第三次把 `Game__TurnTimeoutSeconds` 设成 3600 才量到。
      **「我验的时候它没拦」和「它不拦」是两回事。**
- [x] 初始包 476.84 → **477.83 kB(+0.99)**,预算余量 3.2 → **2.17 kB**。打桩量过:守卫本体占
      **0.91**,路由那层 map 占 **0.08**。
- [x] 把守卫改成 Promise 版(去掉 `from/switchMap/map`)只省 **0.02 kB** —— 所以 rxjs 算子不是那 0.91,
      维持现状。**量了才知道,推不出来。**
- [x] `npm run lint` 0 / `test:ci` **911 绿** / 两个 tsconfig 0 / `build` 0。
- [x] 用完把 API 与 dev server 都停了,`proxy.conf.json` 与 `.claude/launch.json` 从备份还原并确认不在 diff 里,探针 DB 删掉。

## 7. 不做的

- [x] `beforeunload`(刷新 / 关标签页)。房间页刷新会恢复(REST 快照 + 重新入组),另外三条不会 ——
      **触发条件:有人因为刷新丢掉一局方块或一关华容道。**
- [x] 按钮那条判据没有另写:它调的就是 `leaveWarningKey()`。
