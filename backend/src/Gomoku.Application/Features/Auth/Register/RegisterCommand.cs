using Gomoku.Application.Common.DTOs;
using MediatR;

namespace Gomoku.Application.Features.Auth.Register;

/// <summary>注册一个新用户,并即刻签发一对 Access + Refresh Token。</summary>
public sealed record RegisterCommand(
    string Email,
    string Username,
    string Password) : IRequest<AuthResponse>;
