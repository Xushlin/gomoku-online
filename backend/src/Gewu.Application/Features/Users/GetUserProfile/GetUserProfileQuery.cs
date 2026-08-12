using Gomoku.Application.Common.DTOs;
using Gomoku.Domain.Users;
using MediatR;

namespace Gomoku.Application.Features.Users.GetUserProfile;

/// <summary>
/// 按 Id 拉取用户公开主页(不含 Email 等敏感字段)。Bot 账号同样可查 —— 前端回放里链接到
/// <c>AI_Hard</c> 等 bot 时能统一消费。找不到抛 <see cref="Gomoku.Application.Common.Exceptions.UserNotFoundException"/> → 404。
/// </summary>
public sealed record GetUserProfileQuery(UserId UserId) : IRequest<UserPublicProfileDto>;
