using System.Data;
using System.Globalization;
using Dapper;

namespace Cuadre.Application.Database.TypeHandlers;

/// <summary>
/// Business dates ("the day of the close", "purchase date") are LOCAL calendar dates, not instants:
/// a close at 21:00 in Cuba must never become "tomorrow" because of a UTC conversion.
/// Stored as ISO-8601 TEXT ("2026-11-10") so it sorts and compares correctly inside SQLite.
/// </summary>
public sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public const string Format = "yyyy-MM-dd";

    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value.ToString(Format, CultureInfo.InvariantCulture);
    }

    public override DateOnly Parse(object value) => value switch
    {
        DateOnly date => date,
        DateTime dateTime => DateOnly.FromDateTime(dateTime),
        string text => DateOnly.ParseExact(text, Format, CultureInfo.InvariantCulture),
        _ => throw new DataException($"Cannot convert {value.GetType().Name} to DateOnly")
    };
}
