using System.Data.Common;
using Google.Protobuf.WellKnownTypes;

namespace checkmate.Services;

public interface IDatabaseService : IDisposable
{
    DbDataSource DataSource { get; }
    DbParameter CreateParameter(object value);
}