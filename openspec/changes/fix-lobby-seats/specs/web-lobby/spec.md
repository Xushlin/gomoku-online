# web-lobby 的规格变化

## MODIFIED Requirements

### Requirement: 类型化 DTO —— `src/app/core/api/models/` 下的扁平数据类型

DTO 文件 SHALL 独立于 service 文件,放在 `src/app/core/api/models/`:

- `room.model.ts`:
  ```ts
  export type RoomStatus = 'Waiting' | 'Playing' | 'Finished';
  export interface UserSummary { readonly id: string; readonly username: string; }
  export interface RoomSummary {
    readonly id: string;
    readonly name: string;
    readonly status: RoomStatus;
    readonly host: UserSummary;
    readonly black: UserSummary | null;
    /** 全部**在座**的座位。三座位棋种的第三个人只在这里 —— `black` / `white` 读不到他。 */
    readonly seats: readonly { readonly index: number; readonly player: UserSummary }[];
    readonly white: UserSummary | null;
    readonly spectatorCount: number;
    readonly createdAt: string; // ISO8601 from wire; parse lazily if needed
  }
  export interface RoomState { /* shape pinned to backend's RoomStateDto — placeholder page only reads name/host/side; full shape is filled in by add-web-game-board */ }
  ```

- `presence.model.ts`:
  ```ts
  export interface OnlineCountWire { readonly count: number }
  ```
  (service method unwraps this into a plain `number` before handing to caller)

- `leaderboard.model.ts`:
  ```ts
  export interface LeaderboardEntry {
    readonly rank: number;
    readonly userId: string;
    readonly username: string;
    readonly rating: number;
    readonly gamesPlayed: number;
    readonly wins: number;
    readonly losses: number;
    readonly draws: number;
  }
  export interface PagedResult<T> {
    readonly items: readonly T[];
    readonly total: number;
    readonly page: number;
    readonly pageSize: number;
  }
  ```

字段名 MUST 对齐后端实际 wire 形态(camelCase);实施时 MUST 通过读 `backend/src/Gewu.Api/Common/DTOs/*.cs`(或等价)确认 `RoomSummaryDto` 的真实字段名后再 ship。

#### Scenario: 类型收敛到后端
- **WHEN** 实施期对比 `backend/` 下的 DTO 源文件
- **THEN** `RoomSummary` 的每个字段名与后端 DTO 的 JSON 序列化名完全一致(camelCase 对 camelCase)

---

### Requirement: i18n —— `lobby.*` 翻译树同步扩充

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增 `lobby.*` 键集合:

- `lobby.hero.{welcome, online-count-label, online-count-empty}`
- `lobby.rooms.{title, create-button, empty, loading-retry, join, watch, status-waiting, status-playing, status-finished, players, host, spectators}`

  `seat-black` / `seat-white` / `seat-empty` 被 `players` 取代:大厅行渲染**在座的玩家**,
  而 MUST NOT 用颜色说话 —— `board-seats.ts` 自己的文档写着那套读法只有棋盘家族可以调,
  而一个座位数大于二的棋种没有颜色可映。
- `lobby.my-rooms.{title, empty, resume, you-are-seated, you-are-spectator}`

  `you-are-black` / `you-are-white` 被 `you-are-seated` 取代,理由同上;而它修掉的是一条
  真缺陷:三座位房间里 2 号座位上的人此前被标成「你是观战」——**在他自己的对局里**。
- `lobby.leaderboard.{title, empty, rank, rating, wins, losses, draws, tier-gold, tier-silver, tier-bronze}`
- `lobby.create-room.{dialog-title, name-label, name-placeholder, submit, submit-loading, cancel}`
- `lobby.create-room.errors.{min-length, max-length, whitespace-only, generic, network}`
- `lobby.errors.{generic, network, retry}`
- `lobby.placeholder.{coming-soon, leave-room, room-not-found, back-to-lobby}`

两份 JSON 的 flattened key 集合 MUST 完全相等。

#### Scenario: 键集合一致
- **WHEN** 对比 flattened 后的 `en.json` 与 `zh-CN.json`
- **THEN** 差集为空

#### Scenario: 模板零硬编码
- **WHEN** 在 `src/app/pages/lobby/**/*.html` 与 `src/app/pages/rooms/**/*.html` 中搜索 CJK 字符或 ≥ 3 字母的显示英文字符串
- **THEN** 0 匹配(技术 test-id 等非展示字符串除外)

## ADDED Requirements

### Requirement: 大厅的房间行按在座玩家渲染,MUST NOT 假设两个座位

`active-rooms` 的每一行 SHALL 渲染 `room.seats` 里**全部**在座玩家,而 MUST NOT 渲染
写死的两个座位标签。渲染出的玩家链接数 MUST 等于 `seats.length`。

**行上 MUST NOT 出现颜色词。** `board-seats.ts` 的文档写着那套「座位号 → 颜色」的读法
只有棋盘家族可以调用,而一个座位数大于二的棋种没有颜色可映。大厅不是棋盘。

`my-active-rooms` 判断「我在这个房间里是什么身份」SHALL 查 `seats`,而 MUST NOT 只比
`black` / `white`。**「不在座位上」与「在第三个座位上」MUST NOT 得到同一个答案** ——
这与 `fix-three-seat-membership` 在服务端修的是同一句话。

这条 SHALL 由一条**遍历**断言强制:2 个座位与 3 个座位各走一遍,断言渲染出的人数等于
`seats.length`。写成「斗地主房间画三个人」在一个把第三个人硬编码进去的实现上同样是绿的。

375 px 的检查 SHALL 在**三个人名都在行上**时做 —— 两个人名的行过得去,三个未必,而
`generalize-lobby` 已经记过这条:一条「无横向滚动」的检查在空列表上是白过的。

#### Scenario: 三座位房间的第三个人出现在大厅行里
- **WHEN** 一行的 `seats` 有三项
- **THEN** 三个用户名都渲染出来,且都是 `/users/:id` 链接

#### Scenario: 两座位房间不因此多画东西
- **WHEN** 一行的 `seats` 有两项
- **THEN** 恰好两个用户名;行上没有颜色词

#### Scenario: 第三个座位上的人不是观战者
- **WHEN** 当前用户占着 `seats` 里 `index == 2` 的那一项
- **THEN** `my-active-rooms` 说他**在座**,而 MUST NOT 说他在观战
