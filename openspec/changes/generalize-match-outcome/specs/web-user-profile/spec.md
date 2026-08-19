# web-user-profile Specification Delta

## MODIFIED Requirements

### Requirement: 前端类型 `UserPublicProfileDto` / `UserGameSummaryDto` / `PagedResult<T>` 完整化

`src/app/core/api/models/user-profile.model.ts` SHALL 声明:

```ts
export interface UserPublicProfileDto {
  readonly id: string;
  readonly username: string;
  readonly rating: number;
  readonly gamesPlayed: number;
  readonly wins: number;
  readonly losses: number;
  readonly draws: number;
  readonly createdAt: string;
}

export interface UserGameSummaryDto {
  readonly roomId: string;
  readonly name: string;
  readonly black: UserSummary;
  readonly white: UserSummary;
  readonly startedAt: string;
  readonly endedAt: string;
  readonly result: GameResult;
  readonly winnerUserId: string | null;
  readonly endReason: GameEndReason;
  readonly moveCount: number;
}

export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
}
```

字段名与后端 `System.Text.Json` camelCase + `JsonStringEnumConverter` 输出严格对齐。

战绩列表判断一局的胜负 MUST 比较 `winnerUserId` 与被查看用户的 id,MUST NOT 用 `result` 的取值配 `black` / `white` 推断 —— `GameResult` 已不含带颜色的取值。

#### Scenario: 编译通过
- **WHEN** 用上述类型解析真实 API 响应
- **THEN** 无 TypeScript 错误

#### Scenario: 枚举字段按字符串字面量
- **WHEN** 代码写 `summary.result === 'Decided'`
- **THEN** 编译通过;`=== 1` 不通过;`=== 'BlackWin'` 也不通过
