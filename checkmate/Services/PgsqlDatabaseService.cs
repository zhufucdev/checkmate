using System.Data.Common;
using Npgsql;

namespace checkmate.Services;

public class PgsqlDatabaseService : IDatabaseService, IAsyncDisposable
{
    public DbDataSource DataSource { get; }

    public PgsqlDatabaseService(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")!;
        if (connectionString == null)
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
        _createTables();
    }

    private void _createTables()
    {
        using var initBatch = DataSource.CreateBatch();
        var books = initBatch.CreateBatchCommand();
        initBatch.BatchCommands.Add(books);
        books.CommandText =
            """
            create table if not exists books(
                id uuid primary key default gen_random_uuid(),
                name varchar,
                author varchar,
                isbn varchar,
                avatar_uri varchar,
                stock integer
            )
            """;
        var reader = initBatch.CreateBatchCommand();
        reader.CommandText =
            """
            create table if not exists readers
            (
                id            uuid primary key default gen_random_uuid(),
                name          varchar,
                avatar_uri    varchar,
                tier          smallint,
                creditability float
            )
            """;
        initBatch.BatchCommands.Add(reader);
        var borrow = initBatch.CreateBatchCommand();
        borrow.CommandText =
            """
            create table if not exists borrows
            (
                id          uuid primary key default gen_random_uuid(),
                reader_id   uuid,
                book_id     uuid,
                borrow_time timestamp,
                return_time timestamp,
                foreign key (reader_id) references readers (id),
                foreign key (book_id) references books (id)
            )
            """;
        initBatch.BatchCommands.Add(borrow);
        var borrowBatch = initBatch.CreateBatchCommand();
        borrowBatch.CommandText =
            """
            create table if not exists borrow_batches
            (
                id          uuid primary key default gen_random_uuid(),
                reader_id   uuid,
                borrow_time timestamp,
                return_time timestamp,
                foreign key (reader_id) references readers (id)
            )
            """;
        initBatch.BatchCommands.Add(borrowBatch);
        var bookBorrowBatch = initBatch.CreateBatchCommand();
        bookBorrowBatch.CommandText =
            """
            create table if not exists book_borrow_batches
            (
                batch_id uuid,
                book_id  uuid,
                primary key (batch_id, book_id),
                foreign key (batch_id) references borrow_batches (id),
                foreign key (book_id) references books (id)
            )
            """;
        initBatch.BatchCommands.Add(bookBorrowBatch);
        var user = initBatch.CreateBatchCommand();
        user.CommandText =
            """
            create table if not exists users
            (
                id            serial primary key,
                device_name   varchar unique,
                password_hash bytea
            )
            """;
        initBatch.BatchCommands.Add(user);
        var auth = initBatch.CreateBatchCommand();
        auth.CommandText =
            """
                create table if not exists auth
                (
                    id      serial primary key,
                    token   bytea,
                    os      varchar,
                    user_id serial,
                    foreign key (user_id) references users (id)
                )
            """;
        initBatch.BatchCommands.Add(auth);
        initBatch.ExecuteNonQuery();
    }

    public DbParameter CreateParameter(object? value)
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