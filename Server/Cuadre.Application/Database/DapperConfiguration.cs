using Cuadre.Application.Database.TypeHandlers;
using Dapper;

namespace Cuadre.Application.Database;

/// <summary>
/// Global Dapper setup. Single source of truth used by the app AND by the tests,
/// so both always run with exactly the same conventions.
/// </summary>
public static class DapperConfiguration
{
    private static readonly Lock Gate = new();
    private static bool _configured;

    public static void Configure()
    {
        lock (Gate)
        {
            if (_configured)
            {
                return;
            }

            // snake_case columns <-> PascalCase properties without per-query aliases.
            DefaultTypeMap.MatchNamesWithUnderscores = true;

            // GOTCHA: DateTime and Guid are in Dapper's built-in type map, and that map WINS over custom
            // handlers. Without removing them, Dapper hands the raw value to Microsoft.Data.Sqlite, which writes
            // DateTime as "yyyy-MM-dd HH:mm:ss" (no Z) and Guid as a BLOB. Covered by TypeHandlerTests.
            SqlMapper.RemoveTypeMap(typeof(DateTime));
            SqlMapper.RemoveTypeMap(typeof(DateTime?));
            SqlMapper.RemoveTypeMap(typeof(Guid));
            SqlMapper.RemoveTypeMap(typeof(Guid?));
            SqlMapper.RemoveTypeMap(typeof(DateOnly));     // harmless if absent; keeps DateOnly on our handler too
            SqlMapper.RemoveTypeMap(typeof(DateOnly?));

            // SQLite stores dates as TEXT: business dates are DateOnly, audit timestamps are UTC DateTime.
            SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
            SqlMapper.AddTypeHandler(new DateTimeUtcTypeHandler());
            SqlMapper.AddTypeHandler(new GuidTypeHandler());

            _configured = true;
        }
    }
}
