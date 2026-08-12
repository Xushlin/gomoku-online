using Gomoku.Domain.Users;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Gomoku.Infrastructure.Persistence.Converters;

/// <summary><see cref="Email"/> ↔ <see cref="string"/> 的 EF 值转换器。</summary>
public sealed class EmailConverter : ValueConverter<Email, string>
{
    /// <inheritdoc />
    public EmailConverter()
        : base(e => e.Value, v => new Email(v))
    {
    }
}
