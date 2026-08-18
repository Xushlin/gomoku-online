# Tasks — close-bundle-budget

## 1. 先量,再改

- [x] 1.1 `ng build` 现状:**504.65 kB**,超预算 **4.65 kB**(不是 CLAUDE.md 记的 350 bytes)。
- [x] 1.2 归因:检出 `5390ba5` 的 `frontend-web` 重建 → **502.17 kB**。所以 `add-web-tetris` 加了 **2.48 kB**,
      而我在它的记录里写的「did not make it worse」**是错的**。
- [x] 1.3 `ng build --stats-json` + 按 chunk 归属聚合,列出初始包里最大的 30 个输入。

## 2. `@angular/forms` 出 eager 图

- [x] 2.1 确认 eager 消费者**只有** `find-player`(其余 6 处都在懒加载路由后面)。
- [x] 2.2 `FormControl` → `signal` + `[value]` / `(input)`;`toObservable` 接同一条
      `debounceTime(250) → distinctUntilChanged() → trim` 管道。
- [x] 2.3 重建:**470.37 kB**,告警消失。传输量 132.15 → 124.70 kB。

## 3. `StubHub` 绑到契约

- [x] 3.1 `makeRoomState()` 返回真的 `RoomState` —— 补上它缺失的 `gameKey`。
- [x] 3.2 `class StubHub implements GameHubService`,十四个成员用 `vi.fn<T>()` 声明签名。
- [x] 3.3 修正一条靠「`gameKey` 缺失」立论的测试。

## 4. 测试

- [x] 4.1 `find-player` 的 spec 改为驱动真实 `<input>`(它此前戳 `inputCtrl`,那个字段已不存在)。
- [x] 4.2 新增两条:逐字符输入只发一次请求;同一查询不重复请求。
- [x] 4.3 `npm run lint` + `npm run test:ci`:**575 条全绿**(此前 573,+2)。
- [x] 4.4 变异验证。

## 5. 验证

- [x] 5.1 浏览器实测搜索框(见 §7)。
- [x] 5.2 后端零改动。

---

## 6. 三处值得记的东西

### 一、「懒加载路由」不等于「懒加载依赖」

`add-web-tetris` 的初始包增长不是游戏代码,是 `DefaultScoreRunsApiService` 被写进
`app.config.ts` 的 provider 列表 —— 那个文件是 eager 的,于是服务连同它的 import 图一起进初始包,
**无论用它的路由多么懒**。

同一条推理错误会重复发生:每个新的 API service 都要在 `app.config.ts` 里注册一行。所以它进了 spec,
连同它的推论:eager / lazy 的判断只认 `ng build --stats-json` 的 chunk 归属,不认「用它的路由是懒的」。

### 二、34 kB 是一个搜索框的价格

`find-player` 是 `@angular/forms` 唯一的 eager 消费者。它用了一个 `FormControl`,而
登录 / 注册 / 改密码 / 两个对话框 / 聊天面板全在懒加载后面。

换掉那一个 `FormControl` 就够了 —— 而这也说明为什么先量后改:如果照直觉去动 header 的
CDK menu(19 kB)+ overlay(35 kB),那是真的架构改动(菜单要 `@defer`,第一次点击会不开),
而收益还更不确定。**最大的那块不一定是最该动的那块;最该动的是那块「只有一个消费者」的。**

### 三、绑定 `StubHub` 顺带发现了一个静默错误

它被记为「阻塞在 `makeRoomState` 要返回真的 `RoomState`」。做下去发现那个 helper **根本没有
`gameKey`** —— 于是整个文件的房间测试都跑在 `undefined` 上,拿到 `boardSizeFor` 的 15×15 兜底,
而那正好是五子棋的尺寸,所以从来不像出错。

有一条测试还**靠着**这个缺失立论(「客户端认不出的棋种回退到平台首页」)。但 `gameKey` 在线上
不是可选字段,`undefined` 不是服务端能产生的值 —— 那条测试测的是一个不存在的场景。改成一个
未登记的键:同一个意图,真实的场景。

## 7. 实测

### 构建

| | initial 总量 | 传输量 | 告警 |
| --- | --- | --- | --- |
| 改前 | 504.65 kB | 132.15 kB | 超 4.65 kB |
| 改后 | **470.37 kB** | **124.70 kB** | **无** |

`angular.json` 的预算数字**没动**。

### 浏览器(Development,前端 4322 / API 5233)

在 `/home` 上对着真实的搜索框敲,不是 jsdom:

- 每 60 ms 敲一个字符打出 `Tetris` —— 中途卡片文本只有标题,**一个请求都没发**(防抖真的在拦)。
- 900 ms 后出现 `TetrisPlayer 1200`。
- 退回 `Te`(不足 3 字符)→ 显示 `Type 3+ characters to search.`。
- 点结果 → 跳到 `/users/35d5e336-…`,`app-profile-page` 渲染。
- 浏览器后退 → 卡片重新挂载,输入框是**空的**(不残留 `Tetr`)。

### 变异验证

| 改坏什么 | 结果 |
| --- | --- |
| 给 `GameHubService` 加第 15 个抽象成员 | `StubHub` **编译失败**(TS2720),`DefaultGameHubService` 也红(TS2515) |
| —— 这正是绑定之前**不会**发生的事 | 绑定前只有运行时某条测试碰巧走到才会发现 |

`find-player` 那两条新断言本身就是变异检验:去掉 `distinctUntilChanged` → 「同一查询不重复请求」红;
去掉 `debounceTime` → 「逐字符只发一次」红(会发 5 次)。

## 8. 没做的

- **收紧 initial 预算。** 现在 470 kB 对 500 kB,有 30 kB 余量会慢慢被吃掉。把 `maximumWarning`
  降到 480 kB 能把这次的成果锁住,但那是改策略而不是修缺陷,留给它自己的决定。
- **header 的 CDK menu + overlay(约 65 kB)。** 见 §6 二:那是真的架构改动,而且第一次点击
  可能不开菜单。预算已经满足了,所以现在动它是在没有压力的情况下引入 UX 风险。
- **`@angular/core` 的 `_debug_node-chunk`(93.7 kB,初始包里最大的一块)。** 那是框架自己的,
  不是我们能动的。记在这里是为了让下一个看这份清单的人不用重新去查。
