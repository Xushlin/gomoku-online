# package-desktop-exe

把桌面壳打成一个能双击的 Windows exe。

## Why

用户要的。壳本身已经跑通(`add-desktop-shell` 里实测过登录、开房间、实时连接),缺的只是「不装 Node、不敲命令，双击就开」。

`main.ts` 里那行 `join(app.getAppPath(), 'web')` 就是为这一步留的 —— 打包时把 `frontend-web/dist/gewu-web/browser` 拷成应用内的 `web/`,壳一个字不用改。

## 一个必须先量、而不是先假设的风险:asar

electron-builder 默认把应用代码打进 `app.asar` 归档。而协议处理器现在这么取文件:

```ts
const response = await net.fetch(pathToFileURL(asset.path).toString());
```

`existsSync` 在 asar 里是通的(Electron 打过 fs 的补丁),**但一个指向 `…\app.asar\web\index.html` 的 `file://` URL 能不能被 `net.fetch` 解析,是另一回事**。

**这条要实测,不能推。** 猜错的表现是:开发模式一切正常,打出来的 exe 一片白 —— 而那和「构建坏了」长得一模一样,正是这个壳当初拒绝 `file://` 的同一种症状。

三条路,按优先级:

1. **先原样打一次,把 exe 跑起来看。** 通了就什么都不用改。
2. 不通 → `asarUnpack` 把 `web/` 解出来(仍然打包,只是那一部分落在磁盘上)。
3. 再不通 → 整个关掉 asar。

**MUST 先走第 1 条并把结果写下来**,因为 2 和 3 都是在为一个可能不存在的问题付代价。

## 决定

- **electron-builder**,目标 **NSIS 安装包 + portable exe** 两个 —— portable 是「试试」最直接的形式,不装就能跑。
- **不签名。** 没有证书。Windows SmartScreen 会拦一次,这是**预期行为不是缺陷**,要写进文档,否则第一个双击的人会以为程序有毒。
- **产物不进仓库。** `release/` 已在 `.gitignore` 里。

## What changes

- `frontend-desktop/package.json`:electron-builder 依赖 + `build` 配置块 + 一个 `package` 脚本。
- 脚本先构建 Angular、再编译壳、再把产物拷进 `web/`,顺序固定 —— **拷贝必须在 Angular 构建之后**,否则打进去的是上一次的产物,而那种错误在界面上看不出来。

## Non-goals

- **签名与 Microsoft Store 提交** —— 要发布者账号与代码签名证书,**只有用户能提供**。
- **自动更新** —— 触发条件仍是「第一次真要发给别人用」。
- **macOS / Linux 产物** —— 这次只做 Windows;跨平台构建需要在对应平台上跑。
- **不改壳的任何行为。** 打包不该需要改 `main.ts`;如果需要,那说明第 1 条实测失败了,而那要单独记下来。

## 验收

- 打出 exe,**真的双击运行**,确认:窗口开出来、加载的是 `app://`、能登录。
- 记下体积。Electron 运行时解包是 **269 MB**,压缩后的安装包通常小得多 —— **具体数字实测,不猜**。
