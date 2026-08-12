using Gomoku.Application.Common.DTOs;
using MediatR;

namespace Gomoku.Application.Features.Auth.Login;

/// <summary>已注册用户的登录请求。</summary>
public sealed record LoginCommand(string Email, string Password) : IRequest<AuthResponse>;
