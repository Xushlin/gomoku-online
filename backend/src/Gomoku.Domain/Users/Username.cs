using System.Text.RegularExpressions;
using Gomoku.Domain.Exceptions;

namespace Gomoku.Domain.Users;

/// <summary>
/// 用户名值对象。规则:
/// <list type="bullet">
///   <item>长度 3–20 个 UTF-16 字符</item>
///   <item>字符集 <c>[a-zA-Z0-9_]</c> 与中文 BMP <c>[\u4e00-\u9fff]</c></item>
///   <item>不得全部由数字组成</item>
/// </list>
/// 存储保留原始大小写,相等比较大小写不敏感。任一规则违反抛 <see cref="InvalidUsernameException"/>。
/// </summary>
public sealed record Username
{
    private const int MinLength = 3;
    private const int MaxLength = 20;

    private static readonly Regex AllowedCharsPattern = new(
        @"^[a-zA-Z0-9_\u4e00-\u9fff]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>原始大小写的用户名字符串(已校验)。</summary>
    public string Value { get; }

    /// <summary>
    /// 以给定字符串构造 <see cref="Username"/>。
    /// </summary>
    /// <param name="value">待校验的原始字符串。</param>
    /// <exception cref="InvalidUsernameException">长度 / 字符集 / 全数字规则违反。</exception>
    public Username(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidUsernameException("Username must not be null or whitespace.");
        }

        if (value.Length < MinLength || value.Length > MaxLength)
        {
            throw new InvalidUsernameException(
                $"Username length {value.Length} is out of range [{MinLength}..{MaxLength}].");
        }

        if (!AllowedCharsPattern.IsMatch(value))
        {
            throw new InvalidUsernameException(
                "Username contains disallowed characters. Allowed: letters, digits, underscore, Chinese (BMP).");
        }

        if (value.All(char.IsDigit))
        {
            throw new InvalidUsernameException("Username must not consist of digits only.");
        }

        Value = value;
    }

    /// <inheritdoc />
    public bool Equals(Username? other)
    {
        if (other is null)
        {
            return false;
        }
        return string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public override int GetHashCode() => Value.ToLowerInvariant().GetHashCode();

    /// <inheritdoc />
    public override string ToString() => Value;
}
