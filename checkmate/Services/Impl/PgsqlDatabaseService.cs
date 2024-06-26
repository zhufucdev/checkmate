using System.Data.Common;
using Npgsql;

namespace checkmate.Services.Impl;

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
            create table if not exists books
            (
                id         uuid primary key default gen_random_uuid(),
                name       varchar not null,
                author     varchar,
                isbn       varchar,
                avatar_uri varchar,
                stock      integer not null check ( stock > 0 )
            )
            """;
        var reader = initBatch.CreateBatchCommand();
        reader.CommandText =
            """
            create table if not exists readers
            (
                id            uuid primary key default gen_random_uuid(),
                name          varchar not null,
                avatar_uri    varchar,
                tier          smallint not null,
                creditability float not null
            )
            """;
        initBatch.BatchCommands.Add(reader);
        var borrow = initBatch.CreateBatchCommand();
        borrow.CommandText =
            """
            create table if not exists borrows
            (
                id          uuid primary key default gen_random_uuid(),
                reader_id   uuid not null,
                book_id     uuid not null,
                borrow_time timestamp not null,
                due_time    timestamp not null,
                return_time timestamp
            )
            """;
        initBatch.BatchCommands.Add(borrow);
        var borrowBatch = initBatch.CreateBatchCommand();
        borrowBatch.CommandText =
            """
            create table if not exists borrow_batches
            (
                id          uuid primary key default gen_random_uuid(),
                reader_id   uuid not null,
                borrow_time timestamp not null,
                due_time    timestamp not null,
                return_time timestamp
            )
            """;
        initBatch.BatchCommands.Add(borrowBatch);
        var bookBorrowBatch = initBatch.CreateBatchCommand();
        bookBorrowBatch.CommandText =
            """
            create table if not exists book_borrow_batches
            (
                batch_id uuid not null,
                book_id  uuid not null,
                primary key (batch_id, book_id),
                foreign key (batch_id) references borrow_batches (id) on delete cascade
            )
            """;
        initBatch.BatchCommands.Add(bookBorrowBatch);
        var user = initBatch.CreateBatchCommand();
        user.CommandText =
            """
            create table if not exists users
            (
                id            serial primary key,
                device_name   varchar unique not null,
                password_hash bytea,
                role          smallint not null,
                reader_id     uuid references readers (id) on delete set null
            )
            """;
        initBatch.BatchCommands.Add(user);
        var auth = initBatch.CreateBatchCommand();
        auth.CommandText =
            """
                create table if not exists auth
                (
                    id          serial primary key,
                    token       bytea unique not null,
                    os          varchar not null,
                    user_id     serial not null,
                    last_access timestamp default now(),
                    foreign key (user_id) references users (id) on delete cascade
                )
            """;
        initBatch.BatchCommands.Add(auth);
        initBatch.ExecuteNonQuery();
    }

    public DbParameter CreateParameter(object? value, string? typeName = null)
    {
        return new NpgsqlParameter
        {
            Value = value,
            DataTypeName = typeName
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