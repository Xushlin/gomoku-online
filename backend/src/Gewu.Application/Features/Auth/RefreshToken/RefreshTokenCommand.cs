using Gewu.Application.Common.DTOs;
using MediatR;

namespace Gewu.Application.Features.Auth.RefreshToken;

/// <summary>用 refresh token 换一对新 token(访问 + 刷新)。</summary>
public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResponse>;
