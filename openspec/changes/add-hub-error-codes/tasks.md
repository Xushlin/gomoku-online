# Tasks — add-hub-error-codes

## 1. Domain：错误码

- [x] 1.1 `DomainException(string code, string message)` 抽象基类，`Code` 只读，构造时校验 kebab-case。
- [x] 1.2 33 个被 API 有意映射的异常改为继承它（Domain 与 Application 两侧），各给一个码。
- [x] 1.3 象棋的自将/照面走 `InvalidMoveException.SelfCheck(...)`，码是 `self-check`。类型不拆成两个 —— 聚合根与既有测试都以 `InvalidMoveException` 表达「规则拒绝了」，为一句文案改动一条稳定契约不划算。
- [x] 1.4 `DomainErrorCodeTests`：**反射遍历程序集**，断言码非空、kebab-case、两两唯一；另有一条断言遍历本身没走空（一个反射走空的测试会全绿地什么都不验）。

## 2. Api：hub 过滤器

- [x] 2.1 `DomainErrorHubFilter : IHubFilter`，`DomainException` → `HubException(code)`。
- [x] 2.2 `DbUpdateConcurrencyException` → `concurrent-modification`（它不是领域错误，但客户端对它有明确应对：重新拉一次房间状态）。
- [x] 2.3 原始异常连同消息记进服务端日志。
- [x] 2.4 其它异常**不**转换 —— 把未预期的失败包成一个客户端能理解的码，等于声称我们知道它是什么。
- [x] 2.5 `Program.cs` 注册。`AddFilter` 需要全限定：`using Microsoft.Extensions.Logging` 也带了一个同名扩展，短名会撞上。

## 3. 前端

- [x] 3.1 `hubErrorToKey` 改为码 → 键表 + 从 SignalR 的包装里取码；网络判定保留（那是客户端条件，没有服务端码可读）。
- [x] 3.2 新增 i18n 键：`room-not-in-play` / `not-a-player` / `not-opponents-turn` / `invalid-chat`。
- [x] 3.3 `hub-error.mapper.spec.ts` 全部按码断言；新增「线上的包装形式」「一句英文散文只得到 generic」「每个能返回的键在两份 locale 都有文案」。

## 4. 验证

- [x] 4.1 `dotnet build` 0 warning；`dotnet test` **868 passed**（Domain 578 / Application 210 / Infrastructure 80，此前 841）。
- [x] 4.2 `npm run lint` 全绿；`npm run test:ci` **453 passed**（此前 451）。
- [x] 4.3 **Production 模式实测通过** —— 见 §5。
- [x] 4.4 `openspec validate add-hub-error-codes --strict` 通过。

## 5. 归档前必答

- [x] **5.1 Production 下的提示是否与 Development 完全一致？**

  是。同一次非法象棋着法（帥 走斜线）：

  | | 变更前 | 变更后 |
  | --- | --- | --- |
  | Development | That move isn't allowed. | That move isn't allowed. |
  | **Production** | **Something went wrong. Please try again.** | **That move isn't allowed.** |

  「变更前」那一格是本变更存在的全部理由，而它是**测出来的**：同一个构建、同一份数据库，只换 `ASPNETCORE_ENVIRONMENT`。

- [x] **5.2 有没有哪条 hub 可达的错误仍然落到 generic？**

  用长轮询直接读线上帧，在 **Production** 下逐个走：

  | 操作 | 帧 |
  | --- | --- |
  | 帥 走斜线 | `…HubException: invalid-move` |
  | 合法的兵进一 | `{"result": null}` |
  | 紧接着再走一步（已轮到黑方） | `…HubException: not-your-turn` |

  两个不同的码都到位，说明机制不是只对某一条生效。

  **没有在浏览器里复现的**：`self-check`。从开局摆到一个送将局面要走很多步。它由两条测试夹住 —— 后端断言 `InvalidMoveException.SelfCheck(...).Code == "self-check"`，前端断言那个码的包装形式映射到 `game.errors.self-check` —— 而中间那段（过滤器 → 线缆 → 映射）与 `invalid-move` 走的是同一条路径，那条实测过了。这不等于实测了它，所以写在这里。

## 6. 一处我搞错了、并且是实测纠正的

提案初稿写的是「`HubException` 的消息原样送达客户端」。**不对。** SignalR 会包装它:

```
"An unexpected error occurred invoking 'MovePiece' on the server. HubException: invalid-move"
```

结论没变（消息在两种环境下都送达，这正是修法成立的前提），但机制细节错了，而这个错误**有后果**:映射器的第一版拿整串去查表，于是服务端已经在发码、界面上却仍然显示通用错误 —— 一个看起来做完了、其实没做完的修复。

它是怎么被抓住的:浏览器里没变好之后，**直接用长轮询把线上帧读了出来**，而不是继续猜。包装形式与两种环境下逐字节相同这件事，也是同一次测出来的。

> 一个「听起来对」的机制说明，和一个测过的机制说明，差别只有在它错的时候才显出来。
