# web-board-skins Specification Delta

## MODIFIED Requirements

### Requirement: `BoardSkinService` 抽象 DI token + 注册表 + `data-board-skin` 应用

`src/app/core/theme/board-skin.service.ts` SHALL 定义 `abstract class BoardSkinService` 与 `DefaultBoardSkinService` 实现,并通过 `{ provide: BoardSkinService, useClass: DefaultBoardSkinService }` 在 `app.config.ts` 注册(且在 `provideAppInitializer` 中 `inject(BoardSkinService)`,保证首次 paint 前已应用)。组件 MUST 通过抽象类 inject(测试可 stub),MUST NOT 直接 inject 实现类。

API 契约:

```ts
abstract class BoardSkinService {
  abstract readonly skinName: Signal<string>;
  abstract register(name: string, tokens: BoardSkinTokens): void;
  abstract activate(name: string): void;
  abstract availableSkins(): readonly string[];
}
```

`DefaultBoardSkinService` SHALL:

- 构造时注册全部内置 skins,并应用初始 skin。
- 初始 skin 解析顺序:`localStorage('gewu:board-skin')` → 已注册 → 否则默认 `'wood'`;读到未注册的脏值时重写为默认值。
- `activate(name)` MUST 设置 `<html data-board-skin="...">` 并写入 `localStorage`;未注册的 name MUST 被忽略(`console.warn`,不抛错)。
- `localStorage` 读写抛出(隐私模式 / 配额)MUST 静默吞掉。

#### Scenario: 默认 skin
- **WHEN** 全新用户首次打开 app
- **THEN** `skinName()` 返回 `'wood'`;`<html data-board-skin="wood">` 已设置

#### Scenario: 切换持久化
- **WHEN** 调 `activate('classic')`,重启 app
- **THEN** 新一次构造后 `skinName() === 'classic'`,`data-board-skin="classic"` 在首次 paint 前已应用

#### Scenario: 未注册 skin 被忽略
- **WHEN** 调 `activate('nonexistent')`
- **THEN** `skinName()` 不变;不抛错
