using Gomoku.Domain.Users;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Gomoku.Infrastructure.Persistence.Converters;

/// <summary><see cref="Username"/> ↔ <see cref="string"/> 的 EF 值转换器。</summary>
public sealed class UsernameConverter : ValueConverter<Username, string>
{
    /// <inheritdoc />
    public UsernameConverter()
        : base(n => n.Value, v => new Username(v))
    {
    }
}
