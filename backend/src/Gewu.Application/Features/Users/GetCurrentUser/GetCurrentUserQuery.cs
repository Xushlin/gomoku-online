using Gewu.Application.Common.DTOs;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Users.GetCurrentUser;

/// <summary>按 <see cref="UserId"/> 查询当前用户的对外 DTO。</summary>
public sealed record GetCurrentUserQuery(UserId UserId) : IRequest<UserDto>;
