# web-shell Specification Delta

## MODIFIED Requirements

### Requirement: Container vs. Presentational 分层

组件 SHALL 按职责分成两类:

- **Container**:拿数据(通过 service 注入)、编排、分发事件 —— 持有状态与副作用。
- **Presentational**:纯粹通过 `@Input()` 接收数据、通过 `@Output()` 发事件 —— 不注入 service(除了 `ThemeService` / `LanguageService` 这类横切服务),不读路由参数,不触发 HTTP。

一个组件 MUST NOT 同时承担两种职责;超出 200 LOC 的组件 SHALL 拆分或将状态抽到 service。

`Shell`(container,承载 outlet)与 `Header`(container,注入 `ThemeService` + `LanguageService`)示范该分层。

页面级 container 通过 `providers: [...]` 提供自己的数据 service,使其生命周期与页面绑定 —— `Lobby` 提供 `HomeDataService`,`GameLobby` 提供 `LOBBY_GAME_KEY` 与 `LobbyDataService`。

#### Scenario: Shell 是纯 container
- **WHEN** 打开 `src/app/shell/shell.ts`
- **THEN** 它只承载 `<router-outlet>` 与 `Header`,不发起任何 HTTP

#### Scenario: 页面 service 随页面销毁
- **WHEN** 用户离开一个页面级 container
- **THEN** 它 `providers` 提供的数据 service MUST 一同销毁并停掉自己的定时器

#### Scenario: 组件 LOC 上限
- **WHEN** 统计任意单一组件 `.ts` 文件行数
- **THEN** ≤ 200(不含注释/空行可放宽,但模板大小不作为豁免理由 —— 模板过长同样需拆)
