using Gomoku.Domain.Users;
using MediatR;

namespace Gomoku.Application.Features.Auth.ChangePassword;

/// <summary>
/// 修改当前登录用户的密码。要求提供当前密码做二次凭据验证;成功后**吊销全部 refresh token**
/// (其它设备 session 立即失效)。Bot 账号由 Domain 拒绝。
/// </summary>
public sealed record ChangePasswordCommand(
    UserId UserId,
    string CurrentPassword,
    string NewPassword) : IRequest<Unit>;
