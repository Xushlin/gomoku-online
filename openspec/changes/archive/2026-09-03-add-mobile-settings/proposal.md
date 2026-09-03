# add-mobile-settings

一个设置页:主题、深色模式、退出确认。**真机上提的三条,其中一条被量出来是别的东西。**

## 用户在真机上说的

> 2. 现在无法更换主题
> 3. 点击退出没有确认就直接退了
> 4. 设置主题,棋盘颜色,声音的都没有

量下来是这样:

| | 状况 |
| --- | --- |
| 主题 | `defaultThemeName = 'ink'` 和 `themeMode: ThemeMode.dark` **都写死在 `app.dart` 里**。而 `tokens.g.dart` 里 **4 套主题 × light/dark 的值早就同步过来了**,`header.theme.{ink,material,qq-game,system}` 四个名字的文案也在 —— **数据全在,只差一个开关** |
| **棋盘颜色** | **它不是第三件事。** `AppTheme.boardBackground` 读的是主题 token 里的 `color-well` / `color-surface` —— **换主题就换了棋盘颜色**。手机端没有 web 那套独立的 `BoardSkinService` 皮肤轴 |
| 声音 | 手机端**完全没有**音频层。web 的 `SoundService` 是一整套惰性加载的音包注册表 |
| 退出确认 | `IconButton(onPressed: vm.signOut)`,两处,直接调用 |

## 改什么

### 一个设置页,挂在 `/settings`

嵌在 `/` 底下,所以返回键、`canPop` 都跟着已有的三层栈走 —— **不新发明导航**。入口放在目录页的 AppBar(`header.settings.label` 那个键就是「设置」)。

### 主题列表 SHALL 从 `themeTokens` 派生

四个名字**不写在页面里**。`themeTokens` 是一个 `const Map<String, Map<String, Map<String, String>>>`,`keys` 就是全集;而那份产物是 `tool/sync_shared.dart` 从 web 同步来的、`shared_sync_test` 钉着的。

**这是这个仓库修过八次的那类缺陷**,而这里尤其容易犯:四个名字看起来很稳定。走查要断言**每一个 `themeTokens` 的键都有 `header.theme.<key>` 的文案** —— 下次 web 加一套主题,同步过来之后这条会红,而不是页面上多一个渲成原始键的选项。

### 深色模式是**独立的一轴**

`ThemeMode.dark` 现在写死。主题(4)与深浅(2)是**两个正交的轴** —— 与 web 端同一个模型(`ThemeService` 暴露 `themeName` 和 `isDark` 两个信号),不是八个「主题」。

### 退出要先问,而文案一个键都不用加

包里没有专属的退出确认文案,**而不需要新增**:标题用 `header.auth.logout`(「退出登录」),两个按钮用 `lobby.ai-game.cancel`(「取消」)和同一个 `header.auth.logout`。加一个手机端专属的键会让 `shared_sync_test` 红,那条走查存在的理由就是不许有第二套翻译。

### 存在本地

用 `shared_preferences`(新依赖)。**不复用 `flutter_secure_storage`** —— 那是放刷新令牌的地方,把一个主题名字塞进钥匙串是把「秘密」这个词用坏了。

## 不做,各自带触发条件

- **声音。** 手机端没有音频层,web 那套是惰性加载的音包注册表(它当初就是因为 8.69 kB 的首屏开销才改成惰性的)。**触发条件:你确实要在手机上听到落子声** —— 那是独立的一笔,不该塞进「设置页」。
- **独立的棋盘皮肤轴。** 换主题已经换了棋盘颜色。**触发条件:你想要「深色主题 + 木纹棋盘」这种组合** —— 那才需要把 web 的 skin 轴同步过来。
- **切换语言。** `header.language.*` 的文案在包里,但换 locale 要重建 `Translations`(它是在 `AppDependencies.build` 里一次性加载的),那是另一个形状。**触发条件:要支持第二种语言的用户。**

## 规模

一个 `SettingsRepository` + 一个设置页 + `GewuApp` 改成监听设置 + 退出确认。**估计 300–400 行**,大头是测试和那条派生走查。

**`GewuApp` 必须仍然是 `StatelessWidget`** —— 那条是 `add-mobile-router` 用编译器钉住的(一个 tear-off 类型),所以重建靠在 `MaterialApp.router` 外面套一个监听器,而不是把状态搬回外壳。
