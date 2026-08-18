# web-game-board Specification Delta

## RENAMED Requirements

标题本身含平台旧名。应用顺序 RENAMED → REMOVED → MODIFIED → ADDED,所以下面 MODIFIED 用的是新标题。

- FROM: ### Requirement: Hub 连接使用 `/hubs/gomoku` + 查询串 JWT + `AuthService.accessToken()` 工厂
- TO: ### Requirement: Hub 连接使用 `/hubs/match` + 查询串 JWT + `AuthService.accessToken()` 工厂


## MODIFIED Requirements

### Requirement: Hub 连接使用 `/hubs/match` + 查询串 JWT + `AuthService.accessToken()` 工厂

`DefaultGameHubService` SHALL 使用 `HubConnectionBuilder`:

- URL: `'/hubs/match'`
- `accessTokenFactory: () => authService.accessToken() ?? ''` —— 工厂被 SignalR 在每次 connect / auto-reconnect 调用,读当前 `AuthService.accessToken()` signal 的值,保证 token 刷新后的自动重连用最新 token
- `withAutomaticReconnect([0, 2000, 5000, 10000, 30000])` —— 共 5 次重连尝试,时间 0s / 2s / 5s / 10s / 30s
- `configureLogging(LogLevel.Warning)` —— 生产日志级别;调试时可在 dev 环境覆盖

连接 MUST **懒启动**:构造 service 时不连接;只有在首次 `joinRoom()` / `joinSpectatorGroup()` / `makeMove()` / `sendChat()` / `urge()` 被调用时才 `connection.start()`。大厅页 / 其它路由 MUST NOT 触发握手。

`accessToken()` 为 null 时 MUST NOT 触发 connect —— caller(RoomPage)在 `auth.isAuthenticated() === false` 的情况下 MUST NOT 调任何 hub 方法。

#### Scenario: 路过大厅不建立连接
- **WHEN** 登录用户打开 `/home`,在大厅页停留 5 分钟,不进入任何房间
- **THEN** MUST NOT 有任何 WebSocket 握手发往 `/hubs/match`

#### Scenario: 首次 joinRoom 建立连接
- **WHEN** RoomPage `ngOnInit` 调 `hub.joinRoom('r-1')`
- **THEN** MUST 向 `/hubs/match?access_token=<JWT>` 发起一次 WebSocket 握手(auto-reconnect 期间的重试不计入"首次")

#### Scenario: 同连接跨房复用
- **WHEN** 用户从 `/rooms/a` 导航到 `/rooms/b`,期间未重新启动 app
- **THEN** MUST NOT 关闭连接后重建;service MUST 在一次 `leaveRoom('a') → joinRoom('b')` 后继续用同一 `HubConnection`

#### Scenario: token 刷新后自动重连用新 token
- **WHEN** `accessToken` signal 从 `'oldToken'` 被更新到 `'newToken'`(auth interceptor 的 refresh 路径);随后服务端以 token 过期为由关闭连接;auto-reconnect 触发
- **THEN** SignalR 再次握手时的查询串 `?access_token=` MUST = `'newToken'`
