## ADDED Requirements

### Requirement: 手机端 SHALL 按 View → ViewModel → Repository → Service 分层

`lib/` SHALL 分成 `data/`(models / services / repositories)与 `ui/<feature>/`(view / view_model),并遵守:

1. **View MUST NOT 直接使用 Service 或 Dio。** 它只与自己的 ViewModel 说话。
2. **模型 MUST 不可变,且只解析。** 模型里出现网络调用或业务规则,说明它该是别的东西。
3. **ViewModel MUST 是 `ChangeNotifier`,且 MUST NOT 持有 `BuildContext`。** 持有了就不能在没有 widget 的情况下测,而那正是它存在的理由。
4. **JSON → 模型 MUST 只在 Repository 里发生。**
5. **Repository 之外 MUST NOT 有人知道 Dio 存在。**

分层照 Flutter 官方架构指南的 MVVM,不自创。

#### Scenario: 分层边界由走查强制,不是由注释
- **WHEN** 任意 `ui/**` 下的文件 import `data/services/**` 或 `package:dio`
- **THEN** 走查测试 MUST 红
- **AND** 该走查 MUST 有过一次**正面对照**(故意加一条这样的 import 并看到它红)——
  一条没见过红的边界检查等于没有,而写在文档里的分层规则是下一个赶时间的人
  第一个绕过的东西

#### Scenario: 模型不依赖上层
- **WHEN** 任意 `data/models/**` 下的文件 import service 或 repository
- **THEN** 走查测试 MUST 红

### Requirement: token 逻辑 SHALL 住在 Dio 拦截器里,并按路径豁免

认证 SHALL 由 Dio 拦截器实现:附加 token、401 时静默刷新并**只重试一次**。

豁免名单(login / register / refresh)MUST 按**路径**匹配,MUST NOT 按整个 URL 前缀 —— base url 一非空,地址就是绝对的,而 `startsWith('/api/auth/login')` 对绝对地址**恒假**。那会给「本身就是凭据」的三个端点挂上 token,并拿刷新令牌去重试刷新本身。

**这一条 web 端与桌面壳各踩过一次**,所以它是继承来的教训,不是新发现。

重试 MUST NOT 成环:一次刷新、一次重试,失败即失败。成环会把一个过期会话变成对登录端点的请求风暴。

#### Scenario: 凭据端点不带 token
- **WHEN** 请求 login / register / refresh,且地址是绝对的
- **THEN** MUST NOT 带 `Authorization` 头

#### Scenario: 其余请求带 token
- **WHEN** 请求任意其它端点
- **THEN** MUST 带 `Authorization`
- **AND** 这一条与上一条 MUST 同时存在 —— 少了它,一个「从不带 token」的实现也能通过

#### Scenario: 401 只重试一次
- **WHEN** 一个受保护请求连续两次收到 401
- **THEN** MUST 只发生一次刷新与一次重试,之后失败

### Requirement: 重构 MUST NOT 改变任何行为,而判据是既有的端到端切片

`integration_test/play_a_move_test.dart` MUST 通过,且它的**每一个匹配器与期望值 MUST 逐字未变**。

**这是重构不是功能。** 那条测试(注册 → 建房 → 对手加入 → 落子 → 服务端记下 (7,7))已经存在,它就是「什么都没变」的可执行形式 —— 与 `play-from-position` 当初用「既有象棋测试一条不改地通过」是同一手,理由也相同:自己写的新断言证明不了自己没改坏东西。

**判据写的是「匹配器与期望值」而不是「一个字都不改」,因为后者做不到,而写一条做不到的判据只会让人绕过它。** 类型搬了家,取同一个事实的**路径**就得跟着搬:`services.username` → `deps.auth.currentUser?.username`。变的是接收者,不变的是断言什么、期望是什么。实测本次共三行断言的接收者改名,零个期望值改动。

#### Scenario: 期望值未被改动
- **WHEN** 对比本变更与基线
- **THEN** 该文件里 MUST 没有任何匹配器或期望值被修改
- **AND** 接收者路径的改名 MUST 只在类型确实搬家的地方发生,并且逐条能说得出搬到哪了

#### Scenario: 结果一致
- **WHEN** 重构后运行该切片
- **THEN** MUST 仍然是:房间属于本人、状态 `Playing`、**恰好一步且坐标为 (7,7)**
