using Gomoku.Application.Common.DTOs;
using Gomoku.Domain.Users;
using MediatR;

namespace Gomoku.Application.Features.Users.GetCurrentUser;

/// <summary>按 <see cref="UserId"/> 查询当前用户的对外 DTO。</summary>
public sealed record GetCurrentUserQuery(UserId UserId) : IRequest<UserDto>;
