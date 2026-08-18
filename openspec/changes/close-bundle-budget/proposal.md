## Why

`ng build` 一直在报 `bundle initial exceeded maximum budget`。它是 warning 不是 error,所以 CI 一直绿,而这正是它的问题:一个没人处理的告警等于没有告警,而初始包在悄悄长。

三个时间点的实测,不是回忆:

| 时点 | initial 总量 | 超出 |
| --- | --- | --- |
| `generalize-lobby` 之后(记录在 CLAUDE.md 里) | 500.35 kB | 350 bytes |
| `add-web-tetris` **之前**(实测 `5390ba5`) | 502.17 kB | 2.17 kB |
| `add-web-tetris` **之后**(实测 `main`) | 504.65 kB | 4.65 kB |

## 一个我写下的判断被这次测量证伪

`add-web-tetris` 的记录里我写:「did **not** make it worse: the whole game is in lazy chunks, so the initial bundle is unchanged」。

**它加了 2.48 kB。** 原因不是俄罗斯方块的代码进了初始包 —— 那部分确实是懒的 —— 而是 `DefaultScoreRunsApiService` 被写进了 `app.config.ts` 的 provider 列表,而那个文件是 eager 的。

> **「路由是懒加载的」不等于「它用到的东西是懒加载的」。** 一个在 provider 列表里被点名的服务,连同它的 import 图,都在初始包里。

这条推理错误值得写进 spec,因为它会重复发生:每个新增的 API service 都要在 `app.config.ts` 里注册一行。

## 修法:一个组件,34 kB

量了 eager 依赖图(`ng build --stats-json`)之后,最大的一块「不该在这里」是 **`@angular/forms`,34.1 kB**。

它的 eager 消费者**只有一个**:`/home` 上的 `find-player` 卡片,一个带防抖的搜索框,用了一个 `FormControl`。登录/注册页、大厅的两个对话框、房间页的聊天面板 —— 其它所有用 forms 的地方全在懒加载路由后面。

把那一个 `FormControl` 换成 `signal` + `[value]` / `(input)`,行为一字不改(同样的 250 ms 防抖、同样的 3 字符下限、同样的重复查询去重),初始包 **504.65 → 470.37 kB**,告警消失。

deferred 笔记当年的判断——「Closing it needs one small thing rather than an architectural change」——是对的。

## 顺手做掉的第二件 deferred

`StubHub`(`room-page.spec.ts`)是个裸类而不是 `implements GameHubService`,所以给抽象类加成员时这个替身会静静地不完整。绑定它被记为「阻塞在 `makeRoomState` 要返回真的 `RoomState`」。

做下去才发现它为什么重要:那个 helper **根本没有 `gameKey`**,于是整个文件的房间测试都跑在 `undefined` 上,拿到 `boardSizeFor` 的 15×15 兜底 —— 而那正好是五子棋的尺寸,所以从来不像出错。

## What Changes

- `find-player` 卡片去掉 `@angular/forms`,改用 signal 绑定。**行为不变**,它的 spec 改成驱动真实 `<input>` 而不是戳内部字段,并补了两条此前无人断言的行为(防抖只发一次、同一查询不重复请求)。
- `StubHub` 绑到 `GameHubService`;`makeRoomState()` 返回真的 `RoomState`(补上 `gameKey`)。
- `web-shell` 的根路由契约多两段:初始包预算 **只能靠减小 eager 图来满足,不能靠抬高阈值**;以及 eager / lazy 的判断只认构建产物。

## Impact

- 新 requirement:无。`web-shell` 一条 MODIFIED。
- 受影响代码:`find-player.{ts,html,spec.ts}`、`room-page.spec.ts`、`CLAUDE.md`。
- **`angular.json` 的预算数字不动。** 现在有约 30 kB 余量;要不要收紧阈值把这次的成果锁住,是一个独立的策略决定,本变更不替它做。
- **后端零改动。**
