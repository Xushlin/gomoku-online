# tasks — package-desktop-exe

## 0. 先量

- [x] `openspec/changes/` 里没有别的未归档变更。
- [x] `openspec validate package-desktop-exe --strict` 绿。

## 1. 打一次,然后看

- [x] electron-builder + `build` 配置,目标 NSIS + portable。
- [x] 打包脚本顺序:构建 Angular → 编译壳 → 拷 `web/` → 打包。
      **拷贝必须在 Angular 构建之后** —— 顺序错了会把上一次的产物打进去,而界面上看不出来。
- [x] **先按默认(开着 asar)打一次,把 exe 跑起来。** 结果写下来。
- [x] 白屏了才动 asar:先 `asarUnpack` 那一份 `web/`,还不行再整个关掉。
      **MUST NOT 一上来就关 asar** —— 那是在为一个可能不存在的问题付代价。

## 2. 真的双击

- [x] 运行打出来的 exe(不是 `electron .`),确认窗口开出来。
- [x] 确认加载的是 `app://`,不是 `file://`。
- [x] 登录一次 —— 需要一台服务器,用 `gewu.config.json` 或 `GEWU_SERVER` 指过去,
      并且服务端 CORS 白名单里要有 `app://gewu`(见 `desktop-shell` 那条要求)。
- [x] 记体积,**实测不猜**。

## 3. 文档

- [x] 未签名 → SmartScreen 会拦一次,**写进 README**:那是预期行为,不是程序有毒。
- [x] `JOURNAL.md` 一条,含 asar 的实测结果。
- [x] 归档 + `validate --all --strict` 绿。
