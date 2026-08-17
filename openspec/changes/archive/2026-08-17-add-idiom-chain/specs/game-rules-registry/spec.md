# game-rules-registry Specification Delta

## ADDED Requirements

### Requirement: 内置棋种清单是它所需依赖的函数

`BuiltInGameRules.All` SHALL 接受构造内置棋种所需的依赖(当前:`IIdiomLexicon`)并返回全部实例,MUST 保持是**唯一**的一份清单 —— DI 注册与所有「遍历注册表」的测试都从它取。

它此前是一个静态只读列表,因为在此之前每个棋种都能无参构造。成语接龙需要词典,而词典不能在类型初始化时加载。

**MUST NOT 因此把成语接龙单独注册到 DI 而把 `All` 留在原地。** 那正是本仓库已经修过两次的缺陷:一份手写的清单,被某条测试当成注册表。`IsRated ⇒ SupportsHumanVsHuman` 与建房校验的能力检查都遍历 `All`,清单之外的棋种会同时从两条检查里静静溜过去。

改成函数的代价是每个调用方都得说明它拿什么来描述这个平台 —— 那正是诚实的形状。

#### Scenario: 清单含全部内置棋种
- **WHEN** 以一个词典调 `BuiltInGameRules.All(lexicon)`
- **THEN** 返回的实例覆盖平台当前全部已实现的对战棋种,含 `idiom-chain`

#### Scenario: DI 与测试取同一份
- **WHEN** 审阅 `DependencyInjection` 与遍历注册表的各条测试
- **THEN** 它们 MUST 都从 `BuiltInGameRules.All(...)` 取,MUST NOT 各自枚举棋种

#### Scenario: 新棋种自动进入既有的遍历检查
- **WHEN** 往 `All` 添加一个棋种
- **THEN** `IsRated ⇒ SupportsHumanVsHuman` 与建房能力校验 MUST 自动覆盖到它,不需要改动那两处测试
