# tasks — refactor-mobile-mvvm

## 0. 先量基线

- [x] `openspec validate refactor-mobile-mvvm --strict` 绿。
- [x] **先跑一次端到端切片并记下结果** —— 重构后要拿它对比,而「改完再跑」
      证明不了改之前是好的。

## 1. 模型:先把 Map 换掉

- [x] `data/models/`:`AuthUser`、`Room`、`RoomSeat`、`GameSnapshot`、`Move`。
- [x] 不可变(`final` 字段 + `const` 构造器),只有 `fromJson`。
- [x] **模型里不许有网络调用,也不许有业务规则。**
- [x] 手写 `fromJson`,不引代码生成 —— 触发条件写在提案里。

## 2. Dio 与拦截器

- [x] `data/services/dio_client.dart`:baseUrl、超时、拦截器。
- [x] auth 拦截器附加 token,**按路径判断豁免**(login / register / refresh)——
      绝对地址下 `startsWith('/api/auth')` 恒假,这一条 web 端与桌面壳都踩过。
- [x] refresh 拦截器:401 → 静默刷新 → **重试一次,绝不成环**。
- [x] **refresh token 会轮换** —— 换回来的必须存下。
- [x] 用 `http_mock_adapter` 测拦截器,不需要真服务器:
      豁免路径不带 token、其余带、401 只重试一次。

## 3. Repository

- [x] `AuthRepository`、`RoomRepository`。
- [x] **JSON → 模型只在这里发生。**
- [x] Repository 之外没有人 import dio。

## 4. ViewModel 与 View

- [x] 三个 feature 各一个 `ChangeNotifier` ViewModel。
- [x] **ViewModel 不持有 `BuildContext`。**
- [x] View 只渲染 + 转发意图;`context.watch` / `Consumer` 订阅。
- [x] `app.dart` 装 Provider 图。

## 5. 边界要有机制,不是注释

- [x] 一条走查测试读 `lib/` 下每个文件的 import,断言:
      `ui/**` MUST NOT import `data/services/**` 或 `package:dio`;
      `data/models/**` MUST NOT import 任何 service 或 repository。
- [x] **正面对照:故意在一个 View 里 import dio,那条必须红。**
      没见过红的边界检查等于没有。

## 6. 验收:行为不变

- [x] 端到端切片**断言一条不改**地通过(Windows 快,Android 各一次)。
- [x] `flutter analyze` 零问题;单测全绿。
- [x] 记下行数变化 —— 重构变长是正常的,但要说得出多了多少、多在哪。

## 7. 文档

- [x] `CLAUDE.md` 手机端约定:分层图 + 五条规则 + 那条走查的名字。
      **只写规则,不写教程** —— 这个文件每次会话整份加载。
- [x] `JOURNAL.md` 一条。
- [x] 归档 + `validate --all --strict` 绿。
