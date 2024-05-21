using System.Data.Common;
using Google.Protobuf.WellKnownTypes;

namespace checkmate.Services;

public interface IDatabaseService : IDisposable
{
    DbDataSource DataSource { get; }
    public DbParameter CreateParameter(object? value, string? typeName = null);
}