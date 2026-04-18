## 1. 项目脚手架与清理

- [x] 1.1 在 `backend/src/Gomoku.Domain/` 下创建子目录 `Enums/`、`ValueObjects/`、`Entities/`、`Exceptions/`
- [x] 1.2 删除占位文件 `backend/src/Gomoku.Domain/Class1.cs`
- [x] 1.3 删除占位文件 `backend/tests/Gomoku.Domain.Tests/UnitTest1.cs`
- [x] 1.4 在 `Gomoku.Domain.Tests.csproj` 中添加 `FluentAssertions`(最新稳定版)的 `PackageReference`
- [x] 1.5 在 `Gomoku.Domain.Tests.csproj` 的 `<ItemGroup>` 里添加 `<ProjectReference Include="..\..\src\Gomoku.Domain\Gomoku.Domain.csproj" />`,并加一个 `global using FluentAssertions;`(或测试文件内 `using`)

## 2. 枚举与异常

- [x] 2.1 `Enums/Stone.cs`:定义 `public enum Stone { Empty = 0, Black, White }`;加 XML `<summary>` 说明三种状态及 `Empty = 0` 的含义
- [x] 2.2 `Enums/GameResult.cs`:定义 `public enum GameResult { Ongoing = 0, BlackWin, WhiteWin, Draw }`;加 XML 注释
- [x] 2.3 `Exceptions/InvalidMoveException.cs`:继承 `System.Exception`,提供 `(string message)` 与 `(string message, Exception inner)` 两个构造函数,XML 注释说明用途与典型触发场景

## 3. 值对象

- [x] 3.1 `ValueObjects/Position.cs`:用 `public readonly record struct Position(int Row, int Col)`;构造函数内校验 `Row`、`Col` 均在 `[0..14]`,越界抛 `InvalidMoveException`,消息格式:`"Position ({Row}, {Col}) is out of board bounds [0..14]"`
- [x] 3.2 `Position` 上暴露常量 `public const int Size = 15`(或同名的 `BoardSize`)并在 XML 注释里标注"坐标范围"
- [x] 3.3 `ValueObjects/Move.cs`:用 `public readonly record struct Move(Position Position, Stone Stone)`;构造函数校验 `Stone != Stone.Empty`,否则抛 `InvalidMoveException`

## 4. Board 实体

- [x] 4.1 `Entities/Board.cs`:内部字段 `private readonly Stone[] _cells = new Stone[225]`;声明常量 `private const int Size = 15`、`private const int WinLength = 5`
- [x] 4.2 私有方法 `IndexOf(Position)`:`row * Size + col`;隐含假设 `Position` 已校验过范围(值对象保证)
- [x] 4.3 公有方法 `Stone GetStone(Position pos)`:按索引返回数组元素
- [x] 4.4 公有方法 `GameResult PlaceStone(Move move)`:
  - [x] 4.4.1 检查目标格是否已被占据,是则抛 `InvalidMoveException`(消息包含冲突坐标),并保证棋盘状态不变
  - [x] 4.4.2 写入 `_cells`
  - [x] 4.4.3 调用私有判胜方法(见 4.5);若判胜为 `BlackWin`/`WhiteWin`,直接返回
  - [x] 4.4.4 若未决胜且棋盘已满(`_cells` 无 `Empty`),返回 `GameResult.Draw`
  - [x] 4.4.5 否则返回 `GameResult.Ongoing`
- [x] 4.5 私有方法 `bool FormsWin(Position last, Stone color)`:以 `last` 为中心,沿 4 个方向 `(dx,dy)` ∈ `{(0,1),(1,0),(1,1),(1,-1)}` 各向两侧延伸同色子数,任一方向总长(含中心)≥ `WinLength` 即返回 `true`。不做全盘扫描
- [x] 4.6 公有方法 `Board Clone()`:用 `Array.Copy` 深拷贝 `_cells` 到新 `Board` 实例
- [x] 4.7 公有方法 `void Reset()`:`Array.Clear(_cells)`
- [x] 4.8 所有公共 API 加 XML `<summary>`,特别要在 `PlaceStone` 上备注"不捕获异常,调用方应自行校验"

## 5. 单元测试 —— 值对象与枚举

- [x] 5.1 `PositionTests`:合法构造、行越界(`-1`、`15`)、列越界(`-1`、`15`)、相等性(`record struct` 自动实现,验收即可)
- [x] 5.2 `MoveTests`:合法构造、`Stone.Empty` 传入时抛异常
- [x] 5.3 `StoneTests`:`default(Stone) == Stone.Empty` 与 `(int)Stone.Empty == 0`
- [x] 5.4 `GameResultTests`:覆盖枚举四个值存在即可(简短,用来锁定常量名)

## 6. 单元测试 —— Board 基础行为

- [x] 6.1 新建棋盘所有格皆 `Empty`
- [x] 6.2 `PlaceStone` 成功后 `GetStone(pos)` 返回对应颜色
- [x] 6.3 在已有棋子位置落子抛 `InvalidMoveException`,且原有棋子未被覆盖
- [x] 6.4 `PlaceStone` 用越界 `Position` 触发异常(可由 `Position` 构造直接抛出),断言异常类型
- [x] 6.5 `Clone()`:原盘改动不影响副本;副本改动不影响原盘(两个独立用例)
- [x] 6.6 `Reset()`:已落若干子,Reset 后任意位置皆 `Empty`,且可继续落子成功

## 7. 单元测试 —— 判胜

- [x] 7.1 水平连五返回 `BlackWin`(中心附近位置:`(7,3)..(7,7)`)
- [x] 7.2 水平连五返回 `WhiteWin`(位置同上,颜色换白)
- [x] 7.3 垂直连五返回 `BlackWin`
- [x] 7.4 主对角连五(↘)返回 `BlackWin`
- [x] 7.5 反对角连五(↗)返回 `WhiteWin`
- [x] 7.6 长连(6 子同色)仍判胜
- [x] 7.7 上边缘 `(0,0)..(0,4)` 连五获胜
- [x] 7.8 下边缘 `(14,10)..(14,14)` 连五获胜
- [x] 7.9 左边缘 `(10,0)..(14,0)` 竖向连五获胜
- [x] 7.10 右边缘 `(0,14)..(4,14)` 竖向连五获胜
- [x] 7.11 四连尚未连五:返回 `Ongoing`
- [x] 7.12 被对方棋子打断的"四 + 四":`Black Black White Black Black`,最新一手不判胜,返回 `Ongoing`
- [x] 7.13 平局:构造一盘 225 格全填满且无连五的布局,最后一手返回 `Draw`(可用辅助方法按固定图案填充)

## 8. 验证与归档前置检查

- [x] 8.1 `cd backend && dotnet build Gomoku.slnx` 通过,无编译警告视为错误的情况下不引入新警告
- [x] 8.2 `cd backend && dotnet test tests/Gomoku.Domain.Tests` 全部通过
- [x] 8.3 检查 `Gomoku.Domain.csproj`:`<PackageReference>` 与 `<ProjectReference>` 数量均为 0(锁定"零依赖"不变量)
- [x] 8.4 检查 `Gomoku.Domain/` 下**没有**任何 `async`、`.Result`、`.Wait()`、`Task` 等异步痕迹(领域核心纯同步)
- [x] 8.5 运行 `openspec validate add-domain-core` 无错误
- [x] 8.6 按 CLAUDE.md 的"作者自检清单"逐项过一遍;PR 标题用 `feat(domain): add core gomoku board and rules (#<pr-id>)` 风格
