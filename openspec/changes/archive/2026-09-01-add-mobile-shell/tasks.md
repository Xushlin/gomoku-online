# tasks — add-mobile-shell

## 0. 先把最大的风险证伪,再写任何 UI

- [x] `openspec validate add-mobile-shell --strict` 绿。
- [x] **先写一个最小的 SignalR 连通脚本**(Dart,不带 UI):连上真后端的
      `/hubs/match`,带查询串 JWT,`JoinRoom` + `MakeMove` 各成功一次。
      **通了再往下走。** 不通就停下来汇报 —— 那时要决定的是传输方案,
      不是 UI,而铺完 UI 再发现等于白做。
- [x] 把结果写下来:`signalr_netcore` 1.4.4 到底能不能跟我们的 hub 说话。

## 1. 工程与外壳

- [x] `frontend-mobile/` Flutter 工程,Material 3。
- [x] 主题:浅 / 深两套,与 web 端的 token 值对齐(**值对齐,不是重新配色**)。
- [x] i18n **读 web 端那份 JSON**,MUST NOT 建第二套翻译。
      写一条测试:两个 locale 的键集合与 web 端产物完全一致 ——
      少了它,漏一个键的表现是界面上一句原文键。
- [x] 宿主地址:Android 上默认 `http://10.0.2.2:5145`(模拟器里宿主的回环),
      可被覆盖。**在注释里写清为什么不是 localhost。**

## 2. 登录

- [x] 注册 / 登录 / 刷新;token 存 `flutter_secure_storage`。
- [x] refresh token **会轮换** —— 用过的那个必须换掉,否则下次启动落到登录页。
      这一条 web 端踩过,别再踩。
- [x] 401 触发一次静默刷新并重试原请求一次(与 web 端同一条规则)。

## 3. 五子棋一个棋种

- [x] 大厅:房间列表 + 建房 + 加入。
- [x] 棋盘:15×15,落子,实时同步对手的子。
- [x] **服务端判合法性,客户端不判** —— 与 web 端象棋同一条(设计 D2)。

## 4. 真的在模拟器里玩一局

- [x] `flutter emulators --launch pixel_7_-_api_35`,起真后端。
- [x] 注册一个账号、建房、**用第二个客户端**(浏览器 4200 或桌面壳)加入,
      下够五子分出胜负。
- [x] 375 px 等价宽度下不横向溢出 —— 用**最长的真实内容**(20 字符用户名)。

## 5. 文档

- [x] `JOURNAL.md` 一条,含 `signalr_netcore` 的实测结论。
- [x] `CLAUDE.md` 的 mobile 段从「phase 3 / 空」改成实际状态。
- [x] 归档 + `validate --all --strict` 绿。
