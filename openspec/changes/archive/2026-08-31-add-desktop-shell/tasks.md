# tasks — add-desktop-shell

## 0. 先量

- [x] `openspec/changes/` 里没有别的未归档变更。
- [x] `openspec validate add-desktop-shell --strict` 绿。
- [x] 记下改前的前端测试数与初始包大小。

## 1. 协议:唯一真正的难点

- [x] 「URL → 磁盘路径」抽成**纯函数**,不依赖 Electron。
- [x] **拒绝路径穿越**,并且测试里有一条**真的**穿越尝试(`../../..`),
      断言它解析不出 dist 目录 —— 不是断言「函数没抛」。
- [x] 未知路径回落到 `index.html`(SPA 深链),而**不是** 404。
- [x] `<base href="/">` 一个字不改 —— 改它就说明协议那条路没走通。

## 2. 壳

- [x] `frontend-desktop/`,自己的 npm 项目;不建 workspace(仓库没有根 package.json)。
- [x] `contextIsolation: true` / `nodeIntegration: false` / `sandbox: true`。
- [x] preload **只暴露一个只读字符串**,不暴露函数。
- [x] `setWindowOpenHandler` + `will-navigate` 拦住外部导航,并各有一条测试或一次实测。
- [x] CSP 由协议响应头给。

## 3. Web 端那一处

- [x] `API_BASE_URL` 的 factory 读宿主全局;**默认仍是 `''`**。
- [x] 两条测试:全局在场 → 用它;**全局不在场 → 仍是 `''`**。
      少了后一条,一个「永远返回默认」的实现也能通过前一条。
- [x] 既有 1053 条**断言一条不改**地通过。

## 4. 变异

- [x] 去掉穿越检查 → 那条必须红。
- [x] 协议回落改成 404 → 深链那条必须红。
- [x] factory 忽略全局 → 「全局在场」那条必须红。
- [x] **每条都要看到红,且确认是 build 成功之后的红。**

## 5. 真的开起来

- [x] 启动窗口,登录一次,进一个游戏,**确认实时连接活着**(棋盘会动)。
      壳的价值全在「它真的能跑」,没有单测替代品。
- [x] 后端用一台真的服务器(非 4200 / 5145 端口),`Jwt__SigningKey` 要 base64。

## 6. 文档

- [x] `JOURNAL.md` 一条。
- [x] `CLAUDE.md` 的 phase 2 那两行改成「已落地」+ 指向 `frontend-desktop/`。
- [x] 归档 + `validate --all --strict` 绿。
