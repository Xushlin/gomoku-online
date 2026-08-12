using Gomoku.Application.Abstractions;
using Gomoku.Domain.Users;
using IdentityPasswordHasher = Microsoft.AspNetCore.Identity.PasswordHasher<Gomoku.Domain.Users.User>;
using PasswordVerificationResult = Microsoft.AspNetCore.Identity.PasswordVerificationResult;

namespace Gomoku.Infrastructure.Authentication;

/// <summary>
/// 基于 <c>Microsoft.AspNetCore.Identity.PasswordHasher&lt;User&gt;</c>(V3 格式:PBKDF2+HMACSHA512,
/// 100000 次迭代)的 <see cref="IPasswordHasher"/> 实现。本次刻意不对 <c>SuccessRehashNeeded</c>
/// 做自动重哈希(D21):等专门变更 <c>upgrade-password-hash</c> 再处理。
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private readonly IdentityPasswordHasher _inner = new();

    /// <inheritdoc />
    public string Hash(string plainPassword)
    {
        // HashPassword 只把 user 当 salting-tag 使用;传 null 即可。
        return _inner.HashPassword(null!, plainPassword);
    }

    /// <inheritdoc />
    public bool Verify(string plainPassword, string hashed)
    {
        var result = _inner.VerifyHashedPassword(null!, hashed, plainPassword);
        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
