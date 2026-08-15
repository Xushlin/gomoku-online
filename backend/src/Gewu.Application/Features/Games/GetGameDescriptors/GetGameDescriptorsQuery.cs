using Gewu.Application.Common.DTOs;
using MediatR;

namespace Gewu.Application.Features.Games.GetGameDescriptors;

/// <summary>
/// 列出全部已登记的对战棋种及其平台能力。无参数 —— 它是整张注册表的投影。
/// <para>
/// 只覆盖 <c>IGameRules</c>。谜题类走 <c>IPuzzleRules</c>,已经有自己的一条 REST 线;
/// 把两者塞进一个 DTO 会造出一半字段永远为空的形状(谜题没有 Rows / IsRated,对战没有关卡数),
/// 而那种 DTO 的下一步永远是加一个 <c>type</c> 字段然后到处 switch。
/// </para>
/// </summary>
public sealed record GetGameDescriptorsQuery : IRequest<IReadOnlyList<GameDescriptorDto>>;
