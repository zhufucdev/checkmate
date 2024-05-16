using System.Data.Common;

namespace checkmate.Services;

public interface IDatabaseService
{
    DbDataSource DataSource { get; }
}