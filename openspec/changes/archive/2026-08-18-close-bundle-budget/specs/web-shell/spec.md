# web-shell Specification Delta

## MODIFIED Requirements

### Requirement: 根路由契约 —— shell 以外的路由必须懒加载

`app.routes.ts` SHALL 只在 eager 加载列表中包含:(a) shell 布局、(b) 一个占位的 `home` 路由、(c) 必要的 fallback / redirect。所有其它路由 —— 包括此次 scaffold 之后由后续 change 新增的任何业务路由 —— MUST 通过 `loadComponent` 或 `loadChildren` 懒加载。

单个懒加载 chunk 目标 < 200 KB(gzip 后);超出时后续 change 必须拆分,不得在本规范中放宽此阈值。

**初始包同理,而且方向是单向的。** `angular.json` 里配置的 initial 预算 MUST NOT 被放宽来消除告警 —— 超预算时要减小 **eager 依赖图**,而不是抬高阈值。一个被抬高的预算把一个活着的信号变成沉默,而它下一次再响,就是包已经大到没人记得原来多大了。

**「路由是懒加载的」并不等于「它用到的东西是懒加载的」。** 一个在 `app.config.ts` 的 provider 列表里被点名的服务,连同它的 import 图,都在 eager 包里 —— 无论使用它的路由多么懒。同理,被 eager 组件(shell、header、`/home` 的卡片)import 的第三方模块也是 eager 的,即使同一个模块在别处只被懒加载页面用到。判断一个依赖到底在哪一侧,唯一的办法是**量**:`ng build --stats-json` 之后看它落在哪个 chunk 里。

#### Scenario: home 路由在根包中
- **WHEN** 访问 `/`
- **THEN** 初始渲染无需再发起额外 JS chunk 请求即可显示 home 占位页

#### Scenario: 新路由走懒加载
- **WHEN** 任意 `add-web-*` 后续 change 向 `app.routes.ts` 新增业务路由
- **THEN** 该路由 MUST 使用 `loadComponent: () => import(...)` 或 `loadChildren: () => import(...)`,不得直接 `component: XxxComponent`

#### Scenario: 超预算不靠放宽预算解决
- **WHEN** 某次 change 让 initial 包超出 `angular.json` 里配置的预算
- **THEN** 该 change MUST 减小 eager 依赖图,MUST NOT 提高 `maximumWarning` / `maximumError`

#### Scenario: eager 与懒加载的判断只认实测
- **WHEN** 需要断言某个依赖不在初始包里
- **THEN** 依据 MUST 是构建产物(`ng build --stats-json` 的 chunk 归属),MUST NOT 是「用它的路由是懒加载的」这一推理
