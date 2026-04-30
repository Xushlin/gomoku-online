## MODIFIED Requirements

### Requirement: `CreateAiRoomDialog` 提供房间名 + 难度选择 + 提交

`src/app/pages/lobby/dialogs/create-ai-room-dialog/create-ai-room-dialog.{ts,html}` SHALL 渲染一个 CDK Dialog,内含:

- 标题(翻译键 `lobby.ai-game.dialog-title`)
- 房间名输入框 —— 验证规则与 `CreateRoomDialog` 一致:`Validators.required`、`minLength(3)`、`maxLength(50)`、`Validators.pattern(/\S/)`(非全空白);标签和 placeholder 走 `lobby.ai-game.name-label` / `.name-placeholder`
- 难度选择按钮组(`role="radiogroup"`),三个 `role="radio"` 按钮分别对应 `Easy` / `Medium` / `Hard`,labels 走 `lobby.ai-game.difficulty-{easy,medium,hard}`
- **黑白选边按钮组**(本次新增,`role="radiogroup"`),两个 `role="radio"` 按钮 `Black` / `White`,labels 走 `lobby.ai-game.side-{black,white}`,标签头走 `lobby.ai-game.side-label`
- 默认选中 `Medium` 难度与 `Black` 边
- 提交按钮(`lobby.ai-game.submit`,加载中 `lobby.ai-game.submit-loading`)与取消按钮(`lobby.ai-game.cancel`)
- 错误 banner —— 翻译键 `lobby.ai-game.errors.generic` / `.errors.network`,出现在 dialog 顶部

提交 SHALL:

1. 校验表单;无效 → `markAllAsTouched`,不提交。
2. 调 `rooms.createAiRoom(name.trim(), difficulty, humanSide)` —— **三个参数**(本次新增 humanSide 为第三参,见下一条 Requirement)。
3. 成功 → `dialogRef.close(roomState)`(传完整 `RoomState`)。
4. 400 ProblemDetails(name 字段)→ 通过 `mapProblemDetailsToForm` 把字段错误映射到表单(沿用 lobby 现有约定)。
5. 网络错误(status === 0)→ banner `lobby.ai-game.errors.network`。
6. 其它错误 → banner `lobby.ai-game.errors.generic`。

#### Scenario: 默认难度 Medium
- **WHEN** dialog 打开
- **THEN** 难度按钮组中 "Medium" 处于 active(`aria-checked="true"`)状态

#### Scenario: 默认边 Black
- **WHEN** dialog 打开
- **THEN** 边按钮组中 "Black" 处于 active(`aria-checked="true"`)状态

#### Scenario: 难度切换影响出参
- **WHEN** 用户点 "Hard" 按钮,然后输入合法 name 提交
- **THEN** `rooms.createAiRoom(<name>, 'Hard', 'Black')` 被调一次

#### Scenario: 边切换影响出参
- **WHEN** 用户保持难度 Medium,点 "White" 按钮,然后输入合法 name 提交
- **THEN** `rooms.createAiRoom(<name>, 'Medium', 'White')` 被调一次

#### Scenario: 表单非法不发请求
- **WHEN** 用户在 name 输入 "ab"(长度 2)点提交
- **THEN** `rooms.createAiRoom` MUST NOT 被调;name 字段显示 minLength 错误

#### Scenario: 成功关闭传 RoomState
- **WHEN** 后端回 201 + `{ id: 'r-ai-1', ..., game: {...}, ... }`(完整 RoomStateDto)
- **THEN** `dialogRef.close(<roomState>)` 被调一次(参数是收到的对象)

#### Scenario: 网络错误显示 banner
- **WHEN** `createAiRoom` reject `HttpErrorResponse status: 0`
- **THEN** dialog 顶部显示翻译键 `lobby.ai-game.errors.network` 的 banner;表单仍可继续修改 / 重试

---

### Requirement: `RoomsApiService` 增加 `createAiRoom(name, difficulty)` 方法

`src/app/core/api/rooms-api.service.ts` SHALL 在抽象 `RoomsApiService` 类与 `DefaultRoomsApiService` 实现中新增:

```ts
abstract createAiRoom(
  name: string,
  difficulty: BotDifficulty,
  humanSide?: BotSide,
): Observable<RoomState>;
```

`BotSide` 是字符串字面量联合类型 `'Black' | 'White'`,声明在 `src/app/core/api/models/room.model.ts`(可以 alias 现有 `Stone` 类型,排除 `'Empty'`)。

Default 实现 `POST /api/rooms/ai`,body `{ name, difficulty }`(传统 2 字段,缺省时 backend 默认 humanSide=Black)或 `{ name, difficulty, humanSide }`(显式 3 字段)。即:`humanSide` 参数为 undefined 时 body 不带该字段。

#### Scenario: 路径与 body(2 字段)
- **WHEN** 调 `rooms.createAiRoom('Easy match', 'Easy')`(不传 humanSide)
- **THEN** 实际发出 `POST /api/rooms/ai`,body 严格等于 `{ name: 'Easy match', difficulty: 'Easy' }`

#### Scenario: 路径与 body(3 字段)
- **WHEN** 调 `rooms.createAiRoom('Defense', 'Medium', 'White')`
- **THEN** 实际发出 `POST /api/rooms/ai`,body 严格等于 `{ name: 'Defense', difficulty: 'Medium', humanSide: 'White' }`

#### Scenario: 响应形状
- **WHEN** 后端回 201 + 完整 RoomStateDto
- **THEN** Observable emit 该对象;调用方按 `RoomState` 类型消费

---

### Requirement: i18n —— `lobby.ai-game.*` 双语键集合对齐

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增 `lobby.ai-game.*` 子树,至少包含:

- `title`(卡片标题)
- `description`(卡片副标题)
- `button`(主按钮 "New AI game" / "新建 AI 对局")
- `dialog-title`、`name-label`、`name-placeholder`
- `difficulty-label`、`difficulty-easy`、`difficulty-medium`、`difficulty-hard`
- **`side-label`、`side-black`、`side-white`**(本次新增)
- `submit`、`submit-loading`、`cancel`
- `errors.generic`、`errors.network`

flatten 后两份 JSON 的 key 集合 MUST 完全相等(零漂移)。

#### Scenario: parity
- **WHEN** 比对 `en.json` 与 `zh-CN.json` flatten key 集合
- **THEN** 差集为空
