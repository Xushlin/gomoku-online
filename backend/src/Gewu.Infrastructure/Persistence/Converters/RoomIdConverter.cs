using Gewu.Domain.Rooms;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Gewu.Infrastructure.Persistence.Converters;

/// <summary><see cref="RoomId"/> ↔ <see cref="Guid"/> 的 EF 值转换器。</summary>
public sealed class RoomIdConverter : ValueConverter<RoomId, Guid>
{
    /// <inheritdoc />
    public RoomIdConverter()
        : base(id => id.Value, guid => new RoomId(guid))
    {
    }
}
