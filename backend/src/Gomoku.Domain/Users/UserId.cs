namespace Gomoku.Domain.Users;

/// <summary>
/// 用户主键的强类型包装值对象。内部承载一个 <see cref="Guid"/>,
/// 所有 Domain / Application 的公共 API 在引用用户标识时使用 <c>UserId</c> 而非裸 <c>Guid</c>,
/// 避免把"用户 ID"与其他 ID 类型混用。不可变,基于值相等。
/// </summary>
public readonly record struct UserId(Guid Value)
{
    /// <summary>生成一个新的 <see cref="UserId"/>(基于 <see cref="Guid.NewGuid"/>)。</summary>
    public static UserId NewId() => new(Guid.NewGuid());
}
