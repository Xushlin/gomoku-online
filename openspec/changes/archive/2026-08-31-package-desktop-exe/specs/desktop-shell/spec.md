## ADDED Requirements

### Requirement: 打包 SHALL 把 Angular 产物放进应用内的 `web/`,且拷贝在构建之后

打包脚本 SHALL 依次执行:构建 Angular → 编译壳 → 把 `frontend-web/dist/gewu-web/browser` 拷进应用内的 `web/` → 打包。

`main.ts` 的 `webRoot()` 第一候选就是 `join(app.getAppPath(), 'web')`,所以壳**一行都不用改**。

**拷贝 MUST 在 Angular 构建之后。** 顺序反了会把上一次的产物打进去 —— 而那个错误在界面上看不出来:应用照常打开、照常能用,只是少了这次的改动。

#### Scenario: 打包产物里有这次构建的前端
- **WHEN** 改一处前端文案后重新打包
- **THEN** 打出来的应用里 MUST 是新文案

### Requirement: asar 与协议处理器的兼容性 SHALL 实测确定,MUST NOT 预先规避

本变更 SHALL 先按 electron-builder 的默认设置(asar 开启)打包一次并**运行**,再决定要不要动 asar。

风险是具体的:协议处理器用 `net.fetch(pathToFileURL(path))` 取文件,而打包后那个路径在 `app.asar` 里面。`existsSync` 在 asar 里是通的(Electron 打过 fs 补丁),**但 `file://` URL 能不能指进 asar 是另一回事**。

猜错的表现是**打出来的 exe 一片白**,而那与「构建坏了」一模一样 —— 正是这个壳当初拒绝 `file://` 的同一种症状。

处理顺序 MUST 是:原样打 → 跑 → 白屏才 `asarUnpack` 那一份 `web/` → 还不行才整个关掉 asar。**MUST NOT 一上来就关**:那是在为一个可能不存在的问题付代价,而且再没人会去查它到底存不存在。

#### Scenario: 默认配置先试
- **WHEN** 第一次打包
- **THEN** MUST 用默认的 asar 设置,并把运行结果(能开 / 白屏)记录下来

### Requirement: 未签名产物 SHALL 在文档里写明 SmartScreen 会拦

README SHALL 写明这个 exe 没有代码签名,Windows SmartScreen 首次运行会警告,以及那是**预期行为**。

不写的话,第一个双击的人看到的是一个「Windows 已保护你的电脑」弹窗 —— 而那看起来像程序有毒,不像缺一张证书。

签名需要发布者账号与代码签名证书,**不在本变更范围内**。

#### Scenario: 文档说明了警告
- **WHEN** 读 README 的桌面部分
- **THEN** MUST 有一段说明未签名与 SmartScreen
