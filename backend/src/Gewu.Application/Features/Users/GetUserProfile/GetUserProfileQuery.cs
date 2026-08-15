using Gewu.Application.Common.DTOs;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Users.GetUserProfile;

/// <summary>
/// 按 Id 拉取用户在**某一个棋种**上的公开主页(不含 Email 等敏感字段)。Bot 账号同样可查 ——
/// 前端回放里链接到 <c>AI_Hard</c> 等 bot 时能统一消费。
/// 找不到用户抛 <see cref="Gewu.Application.Common.Exceptions.UserNotFoundException"/> → 404;
/// 用户在,但没下过该棋种,返回初始值填的 DTO 而**不是** 404。
/// <para>
/// <see cref="GameKey"/> 是**必填**的;"不带 gameKey 就给五子棋"这个向后兼容缺省只发生在 Api 层。
/// </para>
/// </summary>
public sealed record GetUserProfileQuery(UserId UserId, string GameKey) : IRequest<UserPublicProfileDto>;
