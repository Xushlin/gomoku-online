## ADDED Requirements

### Requirement: SignalR 连通性 SHALL 在写任何 UI 之前被证伪

本变更 SHALL 先用一个不带 UI 的最小 Dart 脚本证明 `signalr_netcore` 能与本平台的 hub 通信:连上 `/hubs/match`、带查询串 JWT、`JoinRoom` 与 `MakeMove` 各成功一次。

**这不是流程洁癖,是这个变更最大的风险。** `signalr_netcore` 是社区包(1.4.4,最近发布 2025-09-05),而我们的 hub 走查询串 JWT + JSON 协议 + 具名方法。它**可能根本不通**,而那时要做的决定是**换传输方案**(自研协议层,或给 hub 加 REST 落子路径)—— 一个比 UI 重写更大的决定。

铺完 UI 再发现,等于那些 UI 白做。所以顺序是规格的一部分。

#### Scenario: 不通就停下来
- **WHEN** 最小脚本无法完成 `JoinRoom` 或 `MakeMove`
- **THEN** 本变更 MUST 停止并汇报,MUST NOT 继续写 UI

#### Scenario: 通了才继续
- **WHEN** 脚本两个调用都成功
- **THEN** 把「能通」这件事写进 JOURNAL,再开始外壳

### Requirement: 手机端 SHALL 读 web 端那份 i18n 产物,MUST NOT 建第二套翻译

手机端 SHALL 使用 `frontend-web/public/i18n/{zh-CN,en}.json` 作为翻译真源。

**547 个键 × 2 个 locale。** 手抄第二套的漂移表现是「同一句话在两个端不一样」,而没有任何东西会报告它 —— 这个仓库为「手抄清单冒充注册表」已经付过**八次**账。

MUST 有一条测试断言两端的键集合**完全一致**(不是「包含」)。漏一个键的表现是界面上出现原文键。

#### Scenario: 键集合一致
- **WHEN** 比较手机端加载的翻译键与 web 端产物
- **THEN** 两个 locale 的键集合 MUST 完全相等
- **AND** 判据 MUST 是相等而不是包含 —— 「包含」在手机端只用了一半键时也成立

### Requirement: Android 上的默认服务器地址 SHALL 是 `10.0.2.2`,而这不是笔误

Android 目标的默认服务器地址 SHALL 是 `http://10.0.2.2:5145`,并 MUST 在代码注释里写明理由。

模拟器里的 `localhost` 是**模拟器自己**;宿主机的回环在模拟器里是 `10.0.2.2`。写 `localhost` 的表现是每个请求都连接被拒,而屏幕上只是登录失败 —— 看起来像后端没起。

这与桌面壳「宿主给地址」是同一个问题,只是答案不同。

#### Scenario: 模拟器连得上宿主的后端
- **WHEN** 在模拟器里登录,后端跑在宿主的 5145
- **THEN** 请求 MUST 到达后端

### Requirement: 手机端 MUST NOT 自行判定走子合法性

棋盘 SHALL 把落子请求发给服务端并接受它的裁决,MUST NOT 在客户端预判合法性。

与 web 端象棋同一条(设计 D2),理由也同一个:客户端持一份规则就是第二份真源,而两份不一致时玩家读到的是「这一步明明能走」。

#### Scenario: 非法落子由服务端拒绝
- **WHEN** 在已有子的交叉点落子
- **THEN** 请求 MUST 被发出,并由服务端的错误码驱动界面提示
