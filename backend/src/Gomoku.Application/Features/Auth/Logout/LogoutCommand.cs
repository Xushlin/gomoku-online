using MediatR;

namespace Gomoku.Application.Features.Auth.Logout;

/// <summary>登出:吊销一枚 refresh token。**幂等** —— 找不到 / 已过期 / 已撤销都静默成功。</summary>
public sealed record LogoutCommand(string RefreshToken) : IRequest<Unit>;
