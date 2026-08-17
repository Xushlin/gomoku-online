# room-and-gameplay Specification Delta

## MODIFIED Requirements

### Requirement: 领域错误带稳定错误码,并以 `HubException` 送达客户端

每一个被 API 有意映射的领域异常 SHALL 继承 `DomainException` 并携带一个稳定的 kebab-case `Code`(如 `not-your-turn`、`invalid-move`、`self-check`、`idiom-not-found`)。

码 MAY 来自**具名静态工厂**而不是一个独立类型:一种拒绝需要自己的文案、却不值得为它多一个异常类型时,`InvalidMoveException.SelfCheck(...)` 那样的工厂是既定做法。成语接龙的三条规则各用一个(`idiom-not-found` / `idiom-does-not-link` / `idiom-already-used`)——「不是成语」「接不上」「说过了」是三种不同的纠正,一个码说不出任何一种。

码是这个错误的**身份**;消息仍然是给日志看的人类散文,MUST NOT 被客户端展示。

SignalR hub SHALL 通过一个过滤器把 `DomainException` 转成 `HubException(code)`,负载**只有码**。

**这不是整洁问题,是一个在生产环境里关掉了的功能。** 一个 hub 方法抛出普通异常时,它的消息只有在 `EnableDetailedErrors` 打开时才会送到客户端,而 `Program.cs` 把它设成 `IsDevelopment()`。因此在 Production 下 SignalR 会把消息换成一句通用文案,客户端此前基于**服务端英文散文**做的关键字匹配全部落空。

实测(同一次非法象棋着法、同一个构建、同一份数据库):

| 环境 | 玩家看到 |
| --- | --- |
| Development | 「That move isn't allowed.」 |
| **Production** | **「Something went wrong. Please try again.」** |

`HubException` 的消息**在两种环境下都会送达** —— 这正是这个类型存在的意义,也是为什么修法不是「在生产打开详细错误」(那会把栈和内部消息一起发给每个客户端)。

但它**不是原样送达**的。实测的线上帧在 `EnableDetailedErrors` 开与关时逐字节相同:

```
"An unexpected error occurred invoking 'MovePiece' on the server. HubException: invalid-move"
```

因此客户端 MUST **从这个包装里取出码**,而不是拿整串去比。规范把它写下来,是因为「消息原样送达」这个说法听起来对、实际不对,而它错了的表现是:服务端已经在发码了,界面上却仍然显示通用错误。

负载只放码而不附带消息,是为了让「展示服务端英文」这件事**做不到**,而不是靠自觉不做。原始异常连同消息 MUST 在服务端记录。

码 MUST 全局唯一。新增一个领域异常时,`DomainException` 的构造函数**强制**它给出一个码 —— 这与「维护一张需要记得扩充的表」不同,后者是纪律,前者是编译器。

#### Scenario: 领域异常在 hub 上变成码
- **WHEN** 一个 hub 方法内部抛出 `NotYourTurnException`
- **THEN** 客户端收到的错误串以 `HubException: not-your-turn` 结尾

#### Scenario: 生产环境送达同样的东西
- **WHEN** `EnableDetailedErrors` 为 false 时重复上一条
- **THEN** 收到的错误串与 Development 下**逐字节相同**

#### Scenario: 服务端英文不出现在负载里
- **WHEN** 抛出的异常带一句具体消息(如 `"A General cannot move from (9, 4) to (7, 4)."`)
- **THEN** 客户端收到的负载 MUST NOT 包含那句消息;它 MUST 出现在服务端日志里

#### Scenario: 非领域异常不被伪装成领域错误
- **WHEN** hub 方法内部抛出一个不继承 `DomainException` 的异常
- **THEN** 过滤器 MUST NOT 把它转成 `HubException`;它按既有方式处理(生产下客户端只得到通用错误)

#### Scenario: 码唯一
- **WHEN** 遍历所有 `DomainException` 子类**以及每一个返回该类型的 public static 工厂方法**
- **THEN** 它们的 `Code` 两两不同,且都非空

#### Scenario: 工厂产出的码也在遍历范围内
- **WHEN** 新增一个像 `SelfCheck` 那样的具名静态工厂,给它一个已被占用的码
- **THEN** 上一条 MUST 失败 —— 遍历只走类型时,`self-check` 从引入起就从未被自己的唯一性断言覆盖过,
  而多三个工厂就是把同一个洞扩大三倍
