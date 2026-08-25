## ADDED Requirements

### Requirement: 对局进行中离开要先确认,而判据由组件给出

`app.routes.ts` 的**每一条**路由 SHALL 挂上 `canDeactivate: [leaveGameGuard]`,而守卫本身 MUST NOT 认识任何路由或组件清单。

守卫读组件上一个**可选**方法:

```ts
export interface ConfirmsLeaving {
  /** 现在离开要警告什么;`null` = 走了不心疼。 */
  leaveWarningKey(): string | null;
}
```

- 方法不存在或返回 `null` → 放行,不弹框。
- 返回一个 i18n 键 → 打开 **CDK Dialog**(MUST NOT 手搓 `<div>` 弹框,也 MUST NOT 用 `window.confirm`),确认才放行。

**守卫挂在每一条路由上,而不是挑游戏路由挂。** 一份「哪些路由算游戏」的手写清单会在第十款游戏落地那天悄悄漏掉一条,而漏掉的表现是**没有弹框**——一个看不出来的缺陷。挂满之后,决定权整个落在组件那一侧,加一款游戏仍然是「落一个文件」。

**每条线的警告文案分开,因为代价不一样。** 一句通用的「确定离开?」会把下面这张表抹平:

| 页面 | `leaveWarningKey()` 非空的条件 | 离开的真实代价 |
| --- | --- | --- |
| 房间页 | `status === 'Playing'` 且**当前用户占着座位** | 离开**不结束对局、也不让出座位**(`ngOnDestroy` 只退 SignalR 组),回合计时继续走 → **超时判负** |
| 华容道关卡 | 关卡进行中且**已经走过至少一步** | 每一步只在客户端,通关才提交 → 全部丢失 |
| 成语纵横关卡 | 已解出至少一个词且未通关 | 每个词虽已提交,但重进是**新的一次尝试**(`StartPuzzleAttempt` 永远新建),用时与提示数从头算 |
| 俄罗斯方块 | 一局进行中(含暂停与提交中) | 成绩在结束时才提交 → 这一局不计入排行 |

**观众 MUST NOT 被拦。** 观众离开不欠任何人;拦住他等于把「你还在局里」这件事说给一个不在局里的人听。同理,**一步没走的关卡 MUST NOT 弹框** —— 点进去看一眼就走是正常操作。

**去 `/login` 的导航 MUST NOT 被拦。** 守卫挂在每一条路由上,而 401 之后是**拦截器**
(不是组件)发起跳转 —— 那条路径绕不过守卫。会话已经过期时问「要离开吗」,而玩家点
「留下」之后留在一个连不上服务端的页面上:**一个把人困住的确认框比没有确认框更糟。**

**弹框那一整块 SHALL 是动态 `import()` 的,连 `Dialog` 一起。** 守卫被路由表引用,所以它在初始包里;而 `@angular/cdk/dialog` 与那个只在点走时才用得上的组件不必跟着进去 —— 预算余量只剩约 3 kB。顺带的好处是 `app.routes.spec.ts` 这种不建 TestBed 的纯单测能直接 import 守卫。

#### Scenario: 会话过期跳登录页不被拦
- **WHEN** 对局进行中收到 401,拦截器导航去 `/login`
- **THEN** 直接放行,MUST NOT 弹框

#### Scenario: 进行中的对局被拦住
- **WHEN** 房间 `status === 'Playing'`、当前用户占着一个座位,此时导航去别处
- **THEN** MUST 先打开 CDK Dialog;点「留下」后 MUST 仍在原路由

#### Scenario: 观众不被拦
- **WHEN** 同一个进行中的房间,但当前用户不占座位
- **THEN** 直接放行,MUST NOT 打开任何弹框

#### Scenario: 没动过的关卡不被拦
- **WHEN** 进入一关华容道、一步没走就离开
- **THEN** 直接放行

#### Scenario: 每条路由都挂着守卫
- **WHEN** 走一遍 `app.routes.ts` 导出的 `routes` 数组
- **THEN** 每一条(含 redirect 与 `**` 兜底)都带 `canDeactivate: [leaveGameGuard]`;新增一条不挂的路由 MUST 让这条测试变红

### Requirement: 「离开房间」也确认一次,而全程只弹一次

房间页的「离开房间」按钮 SHALL 在 `leaveWarningKey()` 非空时先打开**同一个**确认弹框、显示**同一句**文案,确认之后才发 `rooms.leave()` / `rooms.dissolve()`。

**判据 MUST 与守卫共用 `leaveWarningKey()`,MUST NOT 另写一条。** 两条规则会分叉,而分叉的表现是某一条路径悄悄不问了 —— 那是看不出来的。它比误点 header 更贵:离开按钮会**让出座位**,这一局真的结束;而旁边的「认输」本来就要二次确认,两个按钮挨着却只有一个问,不问的那个后果更重。

房间页里凡是**代码发起**的导航 SHALL 经过一个 `leaveTo(url)`,它先把「正在退出」置真再导航;`leaveWarningKey()` 在退出中 SHALL 返回 `null` —— 于是确认过一次之后,守卫不会再问第二次。

覆盖五处:登出跳登录页、房间解散、重连后房间已不在、**点「离开房间」并且服务端已经回成功**、以及结束弹框里的「回大厅 / 看回放」。第四处是要害:`rooms.leave()` 已经发出去了,这时再问「要走吗」是在问一件**已经发生**的事。

**而「记得走 `leaveTo`」不是机制。** 所以 SHALL 有一条检查钉住:`room-page.ts` 里 `router.navigate*` 只许出现在 `leaveTo` 的实现之后。少写一处就变红。

它跑在 **lint** 里(`scripts/check-source-rules.mjs`),不在 vitest 里 —— spec 的 TS 配置没有 `node:fs` 的类型,而读源码文本正是这条检查要做的事。既有的 `check-styles.mjs` 是同一个位置、同一个理由。**检查 MUST 在没有匹配到任何 router 调用时也失败** —— 否则有人把那个调用改名之后,它会安静地对空集合通过。

#### Scenario: 点离开只弹一次
- **WHEN** 对局进行中点「离开房间」,在弹框里确认,服务端回成功并导航走
- **THEN** 弹框**恰好**打开过一次 —— 按钮问过了,守卫 MUST NOT 再问

#### Scenario: 在离开弹框里选留下
- **WHEN** 对局进行中点「离开房间」,在弹框里点「留下」
- **THEN** `rooms.leave` 与 `rooms.dissolve` MUST NOT 被调用,且仍在房间页

#### Scenario: 解散一个等待中的房间不问
- **WHEN** 房主在 `status === 'Waiting'` 的房间点「离开房间」
- **THEN** 直接走 dissolve,MUST NOT 弹框 —— 没有对局可丢

#### Scenario: 房间解散不被拦
- **WHEN** 房主解散房间,`roomDissolved$` 触发导航
- **THEN** 直接放行

#### Scenario: 直接调用路由器会变红
- **WHEN** 在 `room-page.ts` 里绕过 `leaveTo` 直接写一次 `router.navigateByUrl`
- **THEN** `npm run lint` MUST 失败并点名行号

### Requirement: i18n —— 离开确认的双语键

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增 `game.leave-confirm.*` 键集合:标题、四条线各自的正文、「留下」与「离开」两个按钮。

文案 MUST 说出**这一条线**的代价,MUST NOT 是一句通用的「确定离开?」——「超时判负」和「这一局不计入排行」是两件不同的事,而玩家要据此决定。

#### Scenario: 两份 JSON 键集合相等
- **WHEN** flatten 两份 i18n JSON
- **THEN** key 集合完全相等(零漂移),由既有的对齐测试覆盖
