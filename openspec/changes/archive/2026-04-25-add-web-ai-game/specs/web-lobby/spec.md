## ADDED Requirements

### Requirement: 大厅 "Play vs AI" 卡片提供创建 AI 对局入口

Lobby 页 SHALL 在卡片网格右列(与 `find-player`、`my-active-rooms`、`leaderboard` 并列)新增一张 `ai-game` 卡片,代码位于 `src/app/pages/lobby/cards/ai-game/ai-game.{ts,html}` + spec。

卡片渲染:

- 标题(翻译键 `lobby.ai-game.title`)
- 一行说明文案(翻译键 `lobby.ai-game.description`)
- 一个主按钮 "New AI game"(翻译键 `lobby.ai-game.button`)

点击按钮 SHALL 打开 `CreateAiRoomDialog`(CDK Dialog)。Dialog 关闭后:

- 若 `closed` emit 一个 `RoomState` 对象 → `router.navigateByUrl('/rooms/' + state.id)`,**MUST NOT** 再发任何 REST 请求,导航直接进入既存 `RoomPage`。
- 若 `closed` emit `undefined`(取消)→ 不导航,卡片状态保持。

样式契约与其它大厅卡一致:`bg-surface text-text border-border rounded-card shadow-elevated`,无硬编码色值。

#### Scenario: 卡片在大厅渲染
- **WHEN** 登录用户打开 `/home`
- **THEN** 卡片网格中能找到 `ai-game` 卡片(标题翻译键 `lobby.ai-game.title`)

#### Scenario: 创建成功后跳转
- **WHEN** 用户点 "New AI game" 按钮 → dialog 提交合法表单 → 后端回 201 + RoomStateDto
- **THEN** `router.navigateByUrl('/rooms/<roomId>')` 被调一次;dialog 关闭

#### Scenario: 取消不跳转
- **WHEN** 用户点 "New AI game" 按钮 → dialog 取消(关闭 with `undefined`)
- **THEN** `router.navigateByUrl` MUST NOT 被调

---

### Requirement: `CreateAiRoomDialog` 提供房间名 + 难度选择 + 提交

`src/app/pages/lobby/dialogs/create-ai-room-dialog/create-ai-room-dialog.{ts,html}` SHALL 渲染一个 CDK Dialog,内含:

- 标题(翻译键 `lobby.ai-game.dialog-title`)
- 房间名输入框 —— 验证规则与 `CreateRoomDialog` 一致:`Validators.required`、`minLength(3)`、`maxLength(50)`、`Validators.pattern(/\S/)`(非全空白);标签和 placeholder 走 `lobby.ai-game.name-label` / `.name-placeholder`
- 难度选择按钮组(`role="radiogroup"`),三个 `role="radio"` 按钮分别对应 `Easy` / `Medium` / `Hard`,labels 走 `lobby.ai-game.difficulty-{easy,medium,hard}`
- 默认选中 `Medium`(per design D4)
- 提交按钮(`lobby.ai-game.submit`,加载中 `lobby.ai-game.submit-loading`)与取消按钮(`lobby.ai-game.cancel`)
- 错误 banner —— 翻译键 `lobby.ai-game.errors.generic` / `.errors.network`,出现在 dialog 顶部

提交 SHALL:

1. 校验表单;无效 → `markAllAsTouched`,不提交。
2. 调 `rooms.createAiRoom(name.trim(), difficulty)`(`UsersApiService` 不参与;`RoomsApiService` 已有 `create()`,本次新增 `createAiRoom()` 方法)。
3. 成功 → `dialogRef.close(roomState)`(传完整 `RoomState`)。
4. 400 ProblemDetails(name 字段)→ 通过 `mapProblemDetailsToForm` 把字段错误映射到表单(沿用 lobby 现有约定)。
5. 网络错误(status === 0)→ banner `lobby.ai-game.errors.network`。
6. 其它错误 → banner `lobby.ai-game.errors.generic`。

#### Scenario: 默认难度 Medium
- **WHEN** dialog 打开
- **THEN** 难度按钮组中 "Medium" 处于 active(`aria-checked="true"`)状态

#### Scenario: 难度切换影响出参
- **WHEN** 用户点 "Hard" 按钮,然后输入合法 name 提交
- **THEN** `rooms.createAiRoom(<name>, 'Hard')` 被调一次

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
abstract createAiRoom(name: string, difficulty: BotDifficulty): Observable<RoomState>;
```

Default 实现 `POST /api/rooms/ai`,body `{ name, difficulty }`。`BotDifficulty` 是字符串字面量联合类型 `'Easy' | 'Medium' | 'Hard'`,声明在 `src/app/core/api/models/room.model.ts`。

#### Scenario: 路径与 body
- **WHEN** 调 `rooms.createAiRoom('Easy match', 'Easy')`
- **THEN** 实际发出 `POST /api/rooms/ai`,body 严格等于 `{ name: 'Easy match', difficulty: 'Easy' }`

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
- `submit`、`submit-loading`、`cancel`
- `errors.generic`、`errors.network`

flatten 后两份 JSON 的 key 集合 MUST 完全相等。

#### Scenario: parity
- **WHEN** 比对 `en.json` 与 `zh-CN.json` flatten key 集合
- **THEN** 差集为空

---

### Requirement: 颜色 / 组件 / 交互规则继承所有先前立下的约定

`ai-game` 卡片 + `CreateAiRoomDialog` SHALL 遵守 scaffold / lobby / game-board 立下的全部横切规则,MUST NOT 引入任何绕过这些规则的图样:

- 颜色仅 token utilities(无 hex/rgb/hsl,无 `bg-gray-*`)
- 对话框基于 `@angular/cdk/dialog`
- 字符串走 `| transloco`
- HttpClient 仅在 `core/api/rooms-api.service.ts`(已存在)

#### Scenario: 全局 grep 通过
- **WHEN** 在 `pages/lobby/cards/ai-game/`、`pages/lobby/dialogs/create-ai-room-dialog/` 下跑色值 / palette / CJK 三套 grep
- **THEN** 0 匹配
