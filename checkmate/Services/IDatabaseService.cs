using System.Data.Common;

namespace checkmate.Services;

public interface IDatabaseService : IDisposable
{
    DbDataSource DataSource { get; }
    public DbParameter CreateParameter(object? value, string? typeName = null);
}