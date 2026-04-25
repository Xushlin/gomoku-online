## ADDED Requirements

### Requirement: 个人主页 header card 显示在线状态徽章

`ProfilePage` 的 header card SHALL 在 username 左侧渲染一个圆形 presence 徽章:

- 直径约 10px(`h-2.5 w-2.5` 或等价 token utility)
- 在线 → `bg-success`
- 离线 → `bg-muted`
- 仅当 `presence()` 信号 non-null 时渲染(初次加载未完成 / 请求失败时**不**渲染,以避免 UI 噪声)
- 带 `aria-label` 翻译键 `profile.online` / `profile.offline`

数据源:`PresenceApiService.getUserOnline(userId)`,在 `ngOnInit` 与 `getProfile` 并行触发。失败时静默吞掉(不显示 toast / banner / dot),profile 数据正常渲染。无轮询,无重连重试。

#### Scenario: 在线显示绿点
- **WHEN** profile 加载,`getUserOnline` 返回 `true`
- **THEN** dot 元素存在,带 `bg-success` class

#### Scenario: 离线显示灰点
- **WHEN** `getUserOnline` 返回 `false`
- **THEN** dot 元素存在,带 `bg-muted` class

#### Scenario: 请求失败不渲染
- **WHEN** `getUserOnline` 抛出错误(网络 / 500)
- **THEN** dot 元素**不**渲染;profile header 其它字段正常显示;没有 error toast 弹出

#### Scenario: 加载未完成不渲染
- **WHEN** profile 数据已到达,但 presence 请求仍在飞
- **THEN** dot 元素**不**渲染;请求完成后才出现

#### Scenario: aria-label 跟随状态
- **WHEN** 在线 → dot 有 `aria-label`
- **THEN** 翻译值对应 `profile.online` 翻译;离线时对应 `profile.offline`

---

### Requirement: `PresenceApiService.getUserOnline(userId)` 方法

`src/app/core/api/presence-api.service.ts` SHALL 在抽象 `PresenceApiService` 类与 `DefaultPresenceApiService` 实现中新增:

```ts
abstract getUserOnline(userId: string): Observable<boolean>;
```

Default 实现 `GET /api/presence/users/{id}`,返回体形如 `{ userId: string, isOnline: boolean }`,服务层 SHALL 通过 `map(res => res.isOnline)` 把 wire DTO 解包为 boolean。`UserPresenceWire` 接口在 `src/app/core/api/models/presence.model.ts` 声明。

#### Scenario: 路径正确
- **WHEN** 调 `presence.getUserOnline('abc 123')`
- **THEN** 实际发出 `GET /api/presence/users/abc%20123`

#### Scenario: 解包 isOnline 字段
- **WHEN** 后端回 `{ userId: 'u-1', isOnline: true }`
- **THEN** Observable emit `true`,而不是整个对象

---

### Requirement: i18n —— `profile.online` / `profile.offline` 双语对齐

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增以下键:

- `profile.online`(en: "Online" / zh-CN: "在线")
- `profile.offline`(en: "Offline" / zh-CN: "离线")

flatten 后两份 JSON 的 key 集合 MUST 完全相等(零漂移)。

#### Scenario: parity
- **WHEN** 比对 `en.json` 与 `zh-CN.json` flatten key 集合
- **THEN** 差集为空
