using System.Data;
using System.Globalization;
using Dapper;

namespace Cuadre.Application.Database.TypeHandlers;

/// <summary>
/// Audit timestamps (created_at, closed_at...) are INSTANTS: always UTC, stored as ISO-8601 TEXT
/// with the "Z" suffix and read back as DateTimeKind.Utc. SQLite has no date type of its own.
/// </summary>
public sealed class DateTimeUtcTypeHandler : SqlMapper.TypeHandler<DateTime>
{
    public const string Format = "yyyy-MM-ddTHH:mm:ss.fffZ";

    public override void SetValue(IDbDataParameter parameter, DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Timestamps must be UTC (use DateTime.UtcNow)", nameof(value));
        }

        parameter.DbType = DbType.String;
        parameter.Value = value.ToString(Format, CultureInfo.InvariantCulture);
    }

    public override DateTime Parse(object value) => value switch
    {
        DateTime dateTime => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
        string text => DateTime.Parse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal),
        _ => throw new DataException($"Cannot convert {value.GetType().Name} to DateTime")
    };
}
