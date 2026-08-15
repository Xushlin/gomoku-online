using Gewu.Application.Common.DTOs;
using Gewu.Domain.Games.Abstractions;
using MediatR;

namespace Gewu.Application.Features.Games.GetGameDescriptors;

/// <summary>
/// 把 <see cref="IGameRulesRegistry"/> 投影成 <see cref="GameDescriptorDto"/> 列表。
/// <para>
/// **是投影,不是第二份清单。** 注册表加一个棋种,本端点自动多一条;这里 MUST NOT 出现任何
/// "哪些棋种存在"的硬编码 —— 与建房校验不许内联棋种白名单是同一条理由:两份清单迟早不一致,
/// 而不一致的那天不会有人发现。
/// </para>
/// <para>
/// 不访问数据库。注册表本来就在内存里,这是一次纯投影 —— 所以也没有 <c>IUserRepository</c>
/// 之类的依赖可注。
/// </para>
/// <para>
/// 按 <c>GameKey</c> 排序:注册表本身不保证顺序(它是 DI 集合 → 字典),而一个每次刷新都换序的
/// 列表在 UI 上会让人以为数据在变。排序放在这里而不是让客户端排 —— 客户端排就意味着每个
/// 客户端各排一次,而它们迟早排得不一样。
/// </para>
/// </summary>
public sealed class GetGameDescriptorsQueryHandler
    : IRequestHandler<GetGameDescriptorsQuery, IReadOnlyList<GameDescriptorDto>>
{
    private readonly IGameRulesRegistry _rules;

    /// <inheritdoc />
    public GetGameDescriptorsQueryHandler(IGameRulesRegistry rules)
    {
        _rules = rules;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<GameDescriptorDto>> Handle(
        GetGameDescriptorsQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<GameDescriptorDto> items = _rules.All
            .OrderBy(r => r.GameKey, StringComparer.Ordinal)
            .Select(r => new GameDescriptorDto(
                GameKey: r.GameKey,
                IsRated: r.IsRated,
                SupportsHumanVsHuman: r.SupportsHumanVsHuman,
                Rows: r.Rows,
                Cols: r.Cols))
            .ToList()
            .AsReadOnly();

        return Task.FromResult(items);
    }
}
