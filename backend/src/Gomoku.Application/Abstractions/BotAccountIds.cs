using Gomoku.Domain.Ai;

namespace Gomoku.Application.Abstractions;

/// <summary>
/// 机器人账号的固定主键集合。这两个 <see cref="Guid"/> 由 <c>AddBotSupport</c> migration 通过
/// seed 写入 <c>Users</c> 表;Application / Infrastructure 层对 bot 账号的引用 MUST 通过本类间接访问,
/// **不得**在其他代码里硬编码这两个字面量 —— 迁移 + 本文件是唯一允许出现魔法 Guid 的地方。
/// </summary>
public static class BotAccountIds
{
    /// <summary>初级 AI 的固定 UserId。Guid 末尾 <c>ea51</c> ≈ "easy"。</summary>
    public static readonly Guid Easy = Guid.Parse("00000000-0000-0000-0000-00000000ea51");

    /// <summary>中级 AI 的固定 UserId。Guid 末尾 <c>bed10</c> ≈ "bed10"(medium 的四位缩写)。</summary>
    public static readonly Guid Medium = Guid.Parse("00000000-0000-0000-0000-0000000bed10");

    /// <summary>高级 AI 的固定 UserId。Guid 末尾 <c>00ad</c> ≈ "hard" 的缩写(去掉非 hex 字符)。</summary>
    public static readonly Guid Hard = Guid.Parse("00000000-0000-0000-0000-0000000000ad");

    /// <summary>按难度返回对应 bot UserId。未定义的难度抛 <see cref="ArgumentOutOfRangeException"/>。</summary>
    public static Guid For(BotDifficulty difficulty) => difficulty switch
    {
        BotDifficulty.Easy => Easy,
        BotDifficulty.Medium => Medium,
        BotDifficulty.Hard => Hard,
        _ => throw new ArgumentOutOfRangeException(
            nameof(difficulty), difficulty, "No seeded bot account for this difficulty."),
    };

    /// <summary>
    /// 反向查找:若 <paramref name="userId"/> 对应某个 seeded bot,返回其难度;否则返回 <c>null</c>。
    /// 由 <c>ExecuteBotMoveCommandHandler</c> 用来把 <c>UserId</c> 映射回难度以构造 AI。
    /// </summary>
    public static BotDifficulty? TryGetDifficulty(Guid userId)
    {
        if (userId == Easy) return BotDifficulty.Easy;
        if (userId == Medium) return BotDifficulty.Medium;
        if (userId == Hard) return BotDifficulty.Hard;
        return null;
    }
}
