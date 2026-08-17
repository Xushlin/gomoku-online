# platform-catalog Specification Delta

## MODIFIED Requirements

### Requirement: `/games` 是受保护的懒加载游戏目录页

`app.routes.ts` SHALL 新增路由 `games`,带 `canMatch: [authGuard]`,并通过 `loadComponent: () => import(...)` 懒加载 —— 与既有根路由契约一致,MUST NOT 使用 `component:` 直接引用。

未登录用户访问 `/games` MUST 被 `authGuard` 重定向到 `/login?returnUrl=/games`。

`/home` 仍是登录后的落地页,但**不再是五子棋大厅** —— 分棋种的大厅在 `/g/:gameKey/lobby`(见 `web-lobby`)。目录页与 `/home` 的游戏入口条职责不同:目录列全部八款(含规划中)、带描述与内容语言徽标;入口条只列可玩的,是个启动器。

#### Scenario: 懒加载
- **WHEN** 已登录用户从 `/home` 导航到 `/games`
- **THEN** 目录页的 JS chunk 在此刻才被请求,MUST NOT 在应用启动时下载

#### Scenario: 未登录被拦
- **WHEN** 未登录用户直接访问 `/games`
- **THEN** 路由落在 `/login?returnUrl=/games`,目录页 chunk MUST NOT 被下载

---

### Requirement: 目录页为每份清单渲染一张卡片

目录页 SHALL 从 `GameCatalogService.all()` 渲染卡片,每张卡片包含:`icon`、`titleKey` 翻译、`descriptionKey` 翻译、`category` 徽标(`catalog.category-{match,puzzle,score}`)。

- `status === 'available'` 的卡片 SHALL 是导航到 `launchRoute` 的链接。
- `status === 'planned'` 的卡片 SHALL 显示 `catalog.coming-soon` 文案。
- 当活动 locale **不在** `contentLocales` 内时,卡片 SHALL 额外显示 `catalog.chinese-only` 徽标。

模板 MUST NOT 硬编码任何游戏名、描述或状态文案 —— 全部走 Transloco。

五子棋的 `launchRoute` SHALL 是 `/g/gomoku/lobby`。它此前是 `/home`,并在清单里附注说"等泛化大厅那一步再改"——那一步就是这里。**一个棋种的 `launchRoute` MUST 指向属于它自己的页面**,否则目录上的"进入游戏"会把人送到一个跟这个棋种没有绑定关系的地方。

#### Scenario: 卡片数等于清单数
- **WHEN** 注入一个含 N 份清单的 stub catalog 并渲染目录页
- **THEN** 页面渲染 N 张卡片

#### Scenario: 可用游戏可点进
- **WHEN** 渲染一份 `status: 'available'`、`launchRoute: '/g/gomoku/lobby'` 的清单
- **THEN** 该卡片是 `href="/g/gomoku/lobby"` 的链接

#### Scenario: 没有清单再指向 `/home`
- **WHEN** 遍历 `GAME_REGISTRY` 中 `status === 'available'` 的每一份清单
- **THEN** 它的 `launchRoute` MUST NOT 等于 `/home` —— `/home` 是平台主页,不属于任何棋种

#### Scenario: 内容语言不匹配时给出提示
- **WHEN** 活动 locale 为 `en`,清单 `contentLocales` 为 `['zh-CN']`
- **THEN** 该卡片显示 `catalog.chinese-only` 对应文案
