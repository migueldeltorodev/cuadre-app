using Cuadre.Application.Database;
using Cuadre.Application.Database.TypeHandlers;
using Dapper;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace Cuadre.Application.Tests.Database;

/// <summary>
/// Guards the two date conventions of Cuadre against SQLite (which has no date type):
/// business dates as DateOnly "yyyy-MM-dd", audit timestamps as UTC DateTime.
/// The DateTime test also proves Dapper really routes the native DateTime type through our handler.
/// </summary>
public class TypeHandlerTests
{
    static TypeHandlerTests() => DapperConfiguration.Configure();

    private static async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await connection.ExecuteAsync("CREATE TABLE t (day TEXT NOT NULL, at TEXT NOT NULL);");
        return connection;
    }

    [Fact]
    public async Task DateOnly_ShouldBeStoredAsIsoText_AndRoundTrip()
    {
        await using var connection = await OpenAsync();
        var day = new DateOnly(2026, 11, 10);

        await connection.ExecuteAsync("INSERT INTO t VALUES (@day, @at)", new { day, at = DateTime.UtcNow });

        var raw = await connection.ExecuteScalarAsync<string>("SELECT day FROM t");
        raw.Should().Be("2026-11-10");
        var read = await connection.QuerySingleAsync<Row>("SELECT day AS Day, at AS At FROM t");
        read.Day.Should().Be(day);
    }

    [Fact]
    public async Task DateTime_ShouldBeStoredWithZ_AndReadBackAsUtc()
    {
        await using var connection = await OpenAsync();
        var at = new DateTime(2026, 11, 11, 2, 30, 0, DateTimeKind.Utc);

        await connection.ExecuteAsync("INSERT INTO t VALUES (@day, @at)", new { day = new DateOnly(2026, 11, 10), at });

        var raw = await connection.ExecuteScalarAsync<string>("SELECT at FROM t");
        raw.Should().Be("2026-11-11T02:30:00.000Z");
        var read = await connection.QuerySingleAsync<Row>("SELECT day AS Day, at AS At FROM t");
        read.At.Should().Be(at);
        read.At.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void DateTime_ShouldBeRejected_WhenNotUtc()
    {
        var parameter = new SqliteParameter();

        var act = () => new DateTimeUtcTypeHandler().SetValue(parameter, new DateTime(2026, 11, 10, 21, 0, 0, DateTimeKind.Local));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Guid_ShouldBeStoredAsText_InAStrictTextColumn()
    {
        // A STRICT TEXT column rejects the BLOB Microsoft.Data.Sqlite would send without our handler.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await connection.ExecuteAsync("CREATE TABLE g (id TEXT NOT NULL PRIMARY KEY) STRICT;");
        var id = Guid.NewGuid();

        await connection.ExecuteAsync("INSERT INTO g VALUES (@id)", new { id });

        var raw = await connection.ExecuteScalarAsync<string>("SELECT id FROM g");
        raw.Should().Be(id.ToString("D"));
        var read = await connection.QuerySingleAsync<Guid>("SELECT id FROM g");
        read.Should().Be(id);
    }

    private sealed class Row
    {
        public DateOnly Day { get; init; }
        public DateTime At { get; init; }
    }
}
