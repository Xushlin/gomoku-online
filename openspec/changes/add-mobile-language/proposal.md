# 手机端切换语言

## 为什么

两个语言的文案**从第一天就同步在包里**(547 键 × 2 locale,`shared_sync_test` 钉着),`Translations.load(bundle, locale)` 两个都读得出来 —— 只是 `AppDependencies.build` 把 locale 写死成了 `'zh-CN'`,而且加载一次就不再变。

**又是「够不着的能力和不存在的能力在屏幕上长得一样」** —— 和「换不了主题」同一个形状,这是第三次。

## 量到的三件事

1. **文案齐了**:`header.language.label` / `header.language.en` / `header.language.zh-CN` 都在同步产物里。**零新增键。**
2. **`Translations` 现在是 `Provider<Translations>.value` 的一个普通值**,换实例不会让 `context.read` 的调用方重建 —— 所以这一笔的关键不是「存下选择」,是**让整棵树拿到新实例**。
3. **没有任何 View 在 `initState` 里捕获 `Translations`**(查过),所以在 Provider 之上重建就够;否则会出现「切了语言,某几屏还是旧文字」。

## 做什么

- **可选语言从同步产物派生**:`assets/i18n/*.json` 的文件名就是这份注册表。`Translations.supported` 现在是一张**手写的 const 表** —— 那正是这个仓修过九次的形状,所以加一条走查断言两者**相等**。
- `AppSettings` 第五个字段 `locale`,持久化到已有的 `PreferencesStore`。
- **解析顺序:存过的 → 设备语言(若支持) → `zh-CN`**。与 web 的 `localStorage → navigator.language → fallback` 同一个模型。
- 加载是异步的,所以外壳持有一个可监听的 `Translations`,切换时重新 `load` 再整树重建。
- 设置页第五个轴:一组单选,复用 `header.language.*`。

## 判据是屏幕上的字,不是存下的字符串

**这是第三次了**:主题那一笔存得好好的、画的还是旧的;棋盘颜色那一笔断言问的是 token 袋不是屏幕。所以这一笔的主判据是 **`find.text` 在切换后找到的是另一种语言的那句话**。

## 不做

- **第三种语言。** 加一个 locale 就是往 web 那边放一个 JSON 再同步 —— 这一笔不引入新文案。
- **跟随系统实时变化。** 设备语言只在**没有存过选择时**用作回退;人选过之后就以人的选择为准,不因为系统语言变了而改。
- **每屏局部语言。** 语言是平台级的一个轴,和主题一样。
