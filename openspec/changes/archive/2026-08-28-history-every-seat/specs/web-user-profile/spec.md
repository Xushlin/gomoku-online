## MODIFIED Requirements

### Requirement: 前端类型 `UserPublicProfileDto` / `UserGameSummaryDto` / `PagedResult<T>` 完整化

`src/app/core/api/models/user-profile.model.ts` SHALL 声明:

```ts
export interface UserPublicProfileDto {
  readonly id: string;
  readonly username: string;
  readonly rating: number;
  readonly gamesPlayed: number;
  readonly wins: number;
  readonly losses: number;
  readonly draws: number;
  readonly createdAt: string;
}

export interface UserGameSummaryDto {
  readonly roomId: string;
  readonly name: string;
  readonly seats: readonly RoomSeat[];
  readonly startedAt: string;
  readonly endedAt: string;
  readonly result: GameResult;
  readonly winnerUserId: string | null;
  readonly endReason: GameEndReason;
  readonly moveCount: number;
}

export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
}
```

字段名与后端 `System.Text.Json` camelCase + `JsonStringEnumConverter` 输出严格对齐。

战绩列表判断一局的胜负 MUST 比较 `winnerUserId` 与被查看用户的 id,MUST NOT 用 `result` 的取值配 `black` / `white` 推断 —— `GameResult` 已不含带颜色的取值。

#### Scenario: 编译通过
- **WHEN** 用上述类型解析真实 API 响应
- **THEN** 无 TypeScript 错误

#### Scenario: 枚举字段按字符串字面量
- **WHEN** 代码写 `summary.result === 'Decided'`
- **THEN** 编译通过;`=== 1` 不通过;`=== 'BlackWin'` 也不通过

`seats` 取代了 `black` / `white`,理由见 `game-replay`:那两个是 0 / 1 号座位的派生读法,
三座位棋种的战绩里 2 号座位上的人不出现。`RoomSeat` 从 `room.model.ts` 复用,**不新造一份**。

#### Scenario: 类型里没有 black / white
- **WHEN** 审阅 `UserGameSummaryDto`
- **THEN** MUST NOT 有 `black` / `white`;`seats` 的元素类型 MUST 是 `room.model.ts` 那个 `RoomSeat`

---

### Requirement: 个人主页对局列表分页(prev/next)

`ProfilePage` 下半部 SHALL 渲染一个 `games-list` 子组件,显示该用户的对局列表(`GET /api/users/:id/games`),首屏拉 `page=1, pageSize=10`。

每行显示:

- **对手们** —— `seats` 里**除本人以外的每一个座位**,各渲染一个 username 链接
  (`[routerLink]="['/users', <id>]" class="username-link"`,`(click)="$event.stopPropagation()"`)。
  **数量由数据决定,MUST NOT 写死一个:** 此前这里写的是「"对手" = 当前 profile 的 user
  **不是**的那一方」,一个单数的说法,于是三人局里另外两个人只显示得出一个 ——
  而显示出来的是哪一个取决于本人坐 0 号还是别的座位,**读起来像是那一局只有两个人**。
- **我方视角的结果**:见下面那四支。
- **End reason** 翻译(`game.ended.reason-*`)
- **Ended-at** Angular `formatDate`
- **Move count**(纯数字)

整行(除 username link)是一个 `<button>` 或可点击区域,点击 navigate 到 `/replay/:roomId`。

底部分页控件:

- **上一页** 按钮 —— `page === 1` 时 disabled
- **页码指示** —— `Page N of M`,M = `Math.ceil(total / pageSize)`,total === 0 时 M = 1
- **下一页** 按钮 —— `page * pageSize >= total` 时 disabled

切页发起新一次 `getGames(id, page, 10)` 请求,渲染 loading skeleton 直到响应。

**「我方视角的结果」SHALL 分两支,而 MUST NOT 只有胜 / 负 / 平三支。**

- `result === 'Draw'` → `profile.result-draw`
- `winnerUserId === 本人` → `profile.result-win`
- `seats.length === 2` 且赢家不是本人 → `profile.result-loss`
- **其余(三个及以上座位、赢家不是本人)→ `profile.result-unrecorded`**

第四支存在的理由是**这一行说不出那个答案,而说了会是错的**:`WinnerUserId` 只装得下一个座位,
斗地主两名农民却是一起赢的。领域层写明了这个取舍,并把出路留给客户端 ——「客户端从叫分历史里
知道谁是地主」—— 而 `UserGameSummaryDto` 刻意不含 `Moves`,所以那条出路在这一行上不成立。
按旧的三支渲染,**没走出去的那个农民,自己赢的一局显示成「负」**。

一个「说不出」比一个错的答案好,而这条不是工期问题:让服务端算出每人胜负要的是棋种自己的
阵营概念,那笔账的拆除条件是平台需要一条点数阶梯。

#### Scenario: 三人局列出两个对手
- **WHEN** 战绩里有一局三座位对局,本人坐其中一个座位
- **THEN** 那一行**恰好**两个对手 username 链接,`href` 互不相同,且都不是本人

#### Scenario: 两人局列出一个对手
- **WHEN** 同一列表里有一局两座位对局
- **THEN** 那一行**恰好**一个对手链接。**这一条与上一条 MUST 同时存在**

#### Scenario: 三人局里赢家不是本人时不说「负」
- **WHEN** 一局三座位对局,`winnerUserId` 是别人
- **THEN** 渲染 `profile.result-unrecorded`;MUST NOT 渲染 `profile.result-loss`

#### Scenario: 三人局里赢家是本人时照常说「胜」
- **WHEN** 一局三座位对局,`winnerUserId === 本人`
- **THEN** 渲染 `profile.result-win` —— 这一支说得出,就要说

#### Scenario: 两人局照旧说胜负
- **WHEN** 一局两座位对局,赢家不是本人
- **THEN** 渲染 `profile.result-loss`。**反面控制**:第四支 MUST NOT 把两座位的负也吞掉

#### Scenario: 首屏请求
- **WHEN** 用户打开 `/users/u-1`
- **THEN** 同时发起 `getProfile('u-1')` 和 `getGames('u-1', 1, 10)` 两个请求

#### Scenario: 行点击 navigate replay
- **WHEN** 用户点列表第 3 行(`roomId === 'r-x'`)
- **THEN** `router.navigateByUrl('/replay/r-x')` 被调一次

#### Scenario: 对手用户名是链接,不触发行 click
- **WHEN** 用户点列表第 3 行的对手 username
- **THEN** navigate 到 `/users/<opponent.id>`,**不**触发 navigate 到 `/replay/r-x`(stopPropagation 生效)

#### Scenario: 翻页
- **WHEN** 当前 `page=1`,用户点"下一页"
- **THEN** `page` 设为 2;新一次 `getGames('u-1', 2, 10)` 发出;旧数据被替换

#### Scenario: 上一页边界
- **WHEN** `page === 1`
- **THEN** "上一页" 按钮 `disabled`

#### Scenario: 下一页边界
- **WHEN** `total === 25`,`page === 3`,`pageSize === 10`(已经在最后一页)
- **THEN** "下一页" 按钮 `disabled`

#### Scenario: 空战绩
- **WHEN** `getGames` 返回 `items: [], total: 0`
- **THEN** 列表显示翻译键 `profile.games-empty`;翻页按钮全部 disabled
