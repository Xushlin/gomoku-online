using Gomoku.Domain.Users;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Gomoku.Infrastructure.Persistence.Converters;

/// <summary><see cref="UserId"/> ↔ <see cref="Guid"/> 的 EF 值转换器。</summary>
public sealed class UserIdConverter : ValueConverter<UserId, Guid>
{
    /// <inheritdoc />
    public UserIdConverter()
        : base(id => id.Value, guid => new UserId(guid))
    {
    }
}
