using System.Net.Mail;
using Gomoku.Domain.Exceptions;

namespace Gomoku.Domain.Users;

/// <summary>
/// 邮箱值对象。构造时用 <see cref="MailAddress"/> 做基本语法校验,长度 ≤ 254 字符(RFC 5321),
/// 规范化为小写后存储。基于规范化后的字符串做值相等。
/// 非法格式 / 空 / 空白 / 超长会抛 <see cref="InvalidEmailException"/>。
/// </summary>
public sealed record Email
{
    private const int MaxLength = 254;

    /// <summary>规范化后(小写 + 已校验)的邮箱字符串。</summary>
    public string Value { get; }

    /// <summary>
    /// 以给定字符串构造 <see cref="Email"/>。
    /// </summary>
    /// <param name="value">待校验的原始字符串。允许任何大小写,存储时统一小写。</param>
    /// <exception cref="InvalidEmailException">格式非法、为空、超过 254 字符。</exception>
    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidEmailException("Email must not be null or whitespace.");
        }

        if (value.Length > MaxLength)
        {
            throw new InvalidEmailException(
                $"Email exceeds maximum length of {MaxLength} characters (got {value.Length}).");
        }

        string normalized;
        try
        {
            var parsed = new MailAddress(value);
            normalized = parsed.Address;
        }
        catch (FormatException ex)
        {
            throw new InvalidEmailException($"Email has invalid format: '{value}'.", ex);
        }

        var atIndex = normalized.LastIndexOf('@');
        var host = atIndex >= 0 ? normalized[(atIndex + 1)..] : string.Empty;
        if (!host.Contains('.'))
        {
            throw new InvalidEmailException(
                $"Email has invalid format: '{value}' (domain must contain a dot).");
        }

        Value = normalized.ToLowerInvariant();
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
