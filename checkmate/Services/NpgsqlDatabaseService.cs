using System.Data.Common;
using Npgsql;

namespace checkmate.Services;

public class NpgsqlDatabaseService : IDatabaseService
{
    public DbDataSource DataSource { get; }

    public NpgsqlDatabaseService(IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("DefaultConnection");
        if (configuration == null)
        {
            throw new NullReferenceException("DefaultConnection");
        }

        DataSource = NpgsqlDataSource.Create(connection!);

        using var initBatch = DataSource.CreateBatch();
        var books = initBatch.CreateBatchCommand();
        books.CommandText =
            @"create table if not exists books(
    id uuid primary key default gen_random_uuid(),
    name varchar,
    author varchar,
    isbn varchar,
    avatar_uri varchar,
    stock integer
)";
        var reader = initBatch.CreateBatchCommand();
        reader.CommandText =
            @"create table if not exists readers(
    id uuid primary key default gen_random_uuid(),
    name varchar,
    avatar_uri varchar,
    tier smallint,
    creditability float
)";
        var borrow = initBatch.CreateBatchCommand();
        borrow.CommandText =
            @"create table if not exists borrows(
    id uuid primary key default gen_random_uuid(),
    reader_id uuid,
    book_id uuid,
    borrow_time timestamp,
    return_time timestamp,
    foreign key (reader_id) references readers(id),
    foreign key (book_id) references books(id)
)";
        initBatch.ExecuteNonQuery();
    }
}