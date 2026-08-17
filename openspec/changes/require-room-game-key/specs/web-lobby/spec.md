# web-lobby Specification Delta

## MODIFIED Requirements

### Requirement: REST API 服务 —— 抽象类 DI token,典型结构

前端 SHALL 在 `src/app/core/api/` 下提供以下 service,每个 MUST 是 abstract class 作为 DI token(匹配 `AuthService` / `ThemeService` 的模式),由 `Default*ApiService` 实现:

- **`PresenceApiService`**
  - `abstract getOnlineCount(): Observable<number>` — GET `/api/presence/online-count`,把响应的 `{ count }` 解包为纯数字再给调用方。

- **`RoomsApiService`**
  - `abstract list(gameKey: string): Observable<readonly RoomSummary[]>` — GET `/api/rooms?gameKey=<gameKey>`
  - `abstract myActiveRooms(): Observable<readonly RoomSummary[]>` — GET `/api/users/me/active-rooms`(**不**按棋种过滤 —— 它回答"我此刻在哪些局里")
  - `abstract getById(roomId: string): Observable<RoomState>` — GET `/api/rooms/{id}`
  - `abstract create(name: string, gameKey: string): Observable<RoomSummary>` — POST `/api/rooms` `{ name, gameKey }`
  - `abstract join(roomId: string): Observable<RoomState>` — POST `/api/rooms/{id}/join`
  - `abstract leave(roomId: string): Observable<void>` — POST `/api/rooms/{id}/leave`

- **`LeaderboardApiService`**
  - `abstract top(count: number, gameKey: string): Observable<readonly LeaderboardEntry[]>` — GET `/api/leaderboard?gameKey=<gameKey>&page=1&pageSize=<count>`,返回 `items` 数组
  - `abstract getPage(page: number, pageSize: number, gameKey: string): Observable<PagedResult<LeaderboardEntry>>` — 同端点,返回完整 `PagedResult`

`gameKey` 在上述方法上 MUST 是**必填参数**,MUST NOT 有默认值。服务端已不再为它填缺省,而一个可选参数会把「这个调用是给哪个棋种的」重新变成一件读调用点看不出来的事 —— 那正是缺省搬到客户端而不是被删掉。

所有 service MUST `inject(HttpClient)`;各 `Default*ApiService` MUST 用 `@Injectable({ providedIn: 'root' })` 注册,然后在 `app.config.ts` 通过 `{ provide: PresenceApiService, useClass: DefaultPresenceApiService }` 把抽象类绑到实现。

组件 MUST NOT 直接 `inject(HttpClient)`;所有 HTTP 只能从 `src/app/core/api/**/*.ts` 里发出(沿用 `web-shell` 立的规则)。

#### Scenario: 组件通过抽象类拿 service
- **WHEN** `LobbyDataService` 想拉房间列表
- **THEN** 它 `inject(RoomsApiService)`(抽象类),不 `inject(DefaultRoomsApiService)`;测试可提供 stub

#### Scenario: 正确的 URL + 方法
- **WHEN** 各 service 的 method 被调用
- **THEN** 实际发出的 HTTP 请求 method + path 符合上表

#### Scenario: create-room 请求体带棋种
- **WHEN** 调 `rooms.create('My room', 'gomoku')`
- **THEN** 实际发出 `POST /api/rooms` 带 body 严格等于 `{ name: 'My room', gameKey: 'gomoku' }`

#### Scenario: 房间列表带棋种查询串
- **WHEN** 调 `rooms.list('gomoku')`
- **THEN** 实际发出 `GET /api/rooms?gameKey=gomoku`

#### Scenario: 棋种参数没有默认值
- **WHEN** 审阅 `RoomsApiService` 与 `LeaderboardApiService` 的抽象签名
- **THEN** `gameKey` MUST NOT 带 `= '...'` 默认值,也 MUST NOT 是可选参数(`?`)

---

### Requirement: `RoomsApiService` 增加 `createAiRoom(name, difficulty)` 方法

`src/app/core/api/rooms-api.service.ts` SHALL 在抽象 `RoomsApiService` 类与 `DefaultRoomsApiService` 实现中提供:

```ts
abstract createAiRoom(
  name: string,
  difficulty: BotDifficulty,
  humanSide: BotSide,
  gameKey: string,
): Observable<RoomState>;
```

`BotSide` 是字符串字面量联合类型 `'Black' | 'White'`,声明在 `src/app/core/api/models/room.model.ts`。

`gameKey` MUST 必填(服务端不再补缺省)。`humanSide` **也**提升为必填,但理由不同:服务端仍然接受省略它,前端此前每一个调用点都已经在显式传值,所以那个可选性只是签名上的残留,而残留的可选参数会诱使下一个调用点省略它。

Default 实现 `POST /api/rooms/ai`,body 严格等于 `{ name, difficulty, humanSide, gameKey }` —— 四个字段全在,不再按参数是否 `undefined` 拼装。

#### Scenario: 路径与 body
- **WHEN** 调 `rooms.createAiRoom('Defense', 'Medium', 'White', 'gomoku')`
- **THEN** 实际发出 `POST /api/rooms/ai`,body 严格等于 `{ name: 'Defense', difficulty: 'Medium', humanSide: 'White', gameKey: 'gomoku' }`

#### Scenario: 其它棋种
- **WHEN** 调 `rooms.createAiRoom('Xiangqi vs AI', 'Hard', 'Black', 'xiangqi')`
- **THEN** body 中 `gameKey === 'xiangqi'`

#### Scenario: 响应形状
- **WHEN** 后端回 201 + 完整 RoomStateDto
- **THEN** Observable emit 该对象;调用方按 `RoomState` 类型消费
