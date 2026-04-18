namespace Gomoku.Application.Abstractions;

/// <summary>
/// 密码哈希与校验契约。Infrastructure 实现借用
/// <c>Microsoft.AspNetCore.Identity.PasswordHasher&lt;User&gt;</c>(V3 格式:PBKDF2 + HMACSHA512)。
/// 明文密码 MUST NOT 以任何形式落盘、进日志或出现在异常消息里。
/// </summary>
public interface IPasswordHasher
{
    /// <summary>对明文密码生成哈希(每次调用产生不同 salt,哈希结果不同)。</summary>
    string Hash(string plainPassword);

    /// <summary>校验明文密码与已知哈希是否匹配。</summary>
    bool Verify(string plainPassword, string hashed);
}
