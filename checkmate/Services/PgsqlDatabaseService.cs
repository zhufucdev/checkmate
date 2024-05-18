using System.Data.Common;
using Npgsql;

namespace checkmate.Services;

public class PgsqlDatabaseService : IDatabaseService, IAsyncDisposable
{
    public DbDataSource DataSource { get; }

    public PgsqlDatabaseService(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")!;
        if (configuration == null)
        {
            throw new NullReferenceException("DefaultConnection");
        }

        var dbName = new NpgsqlConnectionStringBuilder
        {
            ConnectionString = connectionString
        }.Database;
        if (dbName != null)
        {
            using var administrative = new NpgsqlConnection(
                new NpgsqlConnectionStringBuilder
                {
                    ConnectionString = connectionString,
                    Database = "postgres"
                }.ConnectionString
            );
            administrative.Open();
            using var queryCommand = administrative.CreateCommand();
            queryCommand.CommandText = "select * from pg_database where datname = $1";
            queryCommand.Parameters.AddWithValue(dbName);

            if (queryCommand.ExecuteScalar() == null)
            {
                using var createDb = administrative.CreateCommand();
                createDb.CommandText = $"create database {dbName}";
                createDb.ExecuteNonQuery();
            }
        }

        DataSource = NpgsqlDataSource.Create(connectionString);

        using var initBatch = DataSource.CreateBatch();
        var books = initBatch.CreateBatchCommand();
        initBatch.BatchCommands.Add(books);
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
        initBatch.BatchCommands.Add(reader);
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
        initBatch.BatchCommands.Add(borrow);
        initBatch.ExecuteNonQuery();
    }

    public DbParameter CreateParameter(object value)
    {
        return new NpgsqlParameter
        {
            Value = value
        };
    }

    public void Dispose()
    {
        DataSource.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();
    }
}