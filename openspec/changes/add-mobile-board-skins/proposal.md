# 手机端的棋盘皮肤

## 为什么

「棋盘的风格应该能改,和 web 端一样。」

web 端有三个皮肤(`wood` / `classic` / `midnight`),由 `BoardSkinService` 注册名字、`styles/board-skins.css` 提供值。手机端没有 —— 而且**现在的 live 规格里明写着不许有**:

> **AND** 手机端 MUST NOT 另建一条独立的棋盘皮肤轴 —— 那是 web 的 `BoardSkinService`

那句话是 `add-mobile-settings` 写的,而它配的判据是「四个主题给出的棋盘底色不止一种,**哪天它们变得一样,那才是需要独立皮肤轴的日子**」。今天触发它的不是那个判据,是一句需求 —— 所以这一笔**推翻一条 live 要求**,走 `MODIFIED`,而不是悄悄加一个和它矛盾的能力。**这个仓为「live 规格与代码相反」付过 36 个提交的账。**

## 量到的三件事,决定了这一笔能做到什么程度

1. **web 的皮肤名在服务里,值在 CSS 里** —— 每个皮肤块 **43 个变量**。
2. **其中相当一部分是多层 CSS 渐变**(木纹是 vignette + 两层 `repeating-linear-gradient` + 一个 `radial-gradient` 叠出来的)。**Flutter 里没有 CSS 解析器,这部分不可能 1:1 搬过来。**
3. **手机端的棋子颜色是写死在 renderer 里的**(`0xFF1A1A1A` / `0xFFF5F5F5`),线色借的是 `dividerColor` —— 这三样正是皮肤本该拥有的东西。

## 做什么

- **扩 `tool/sync_shared.dart`**:它已经在解析 `[data-theme='…']`,再解析 `[data-board-skin='…']`,生成 `lib/theme/skin_tokens.g.dart`。**皮肤名单从产物的键派生,不手写** —— 这个仓修过九次「手写清单假装成注册表」。
- `BoardSkin`:背景、格线、星位、两种棋子及其描边 —— 从产物里取色。
- 画笔改成收一个 `BoardSkin`,**renderer 里不再有写死的颜色**。
- 设置页第四个轴:皮肤选择,复用 `header.board-skin.*`(**三个名字的文案都已在同步产物里,零新增键**)。
- 持久化到已有的 `PreferencesStore`。

## 明确做不到的:渐变

**这一笔搬的是调色板,不是纹理。** 木纹的布纹感、暗角、交叉纹理是 CSS 多层渐变,Flutter 端会用一个近似的 `RadialGradient`(基色高光 + 本体)代替 —— **两端不会像素级一致,而这句话必须写在这里而不是事后解释**。可搬的是颜色值,而颜色值恰好是能断言的。

## 不做

- **让两端像素级一致。** 见上。
- **每个棋种自己的皮肤。** 皮肤是平台级的一个轴,和主题一样。
- **iOS 验证。** 手上只有安卓机。
