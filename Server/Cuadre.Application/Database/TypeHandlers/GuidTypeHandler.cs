using System.Data;
using Dapper;

namespace Cuadre.Application.Database.TypeHandlers;

/// <summary>
/// Ids are stored as lowercase TEXT ("3f2504e0-4f89-11d3-9a0c-0305e82c3301"), readable in any SQLite browser.
/// Without this handler Microsoft.Data.Sqlite binds Guid as a 16-byte BLOB, which a STRICT TEXT column rejects.
/// </summary>
public sealed class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override void SetValue(IDbDataParameter parameter, Guid value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value.ToString("D");
    }

    public override Guid Parse(object value) => value switch
    {
        Guid guid => guid,
        string text => Guid.Parse(text),
        byte[] bytes => new Guid(bytes),
        _ => throw new DataException($"Cannot convert {value.GetType().Name} to Guid")
    };
}
