using System.Collections.ObjectModel;
using System.Data.Common;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Sqlmaster.Protobuf;

namespace checkmate.Services;

public class LibraryService(IDatabaseService db, IAuthenticatorService authenticator) : Library.LibraryBase
{
    private static readonly ContinuityStreamOwner<GetBooksResponse> BookContinuity = new();
    private static readonly ContinuityStreamOwner<GetBorrowsResponse> BorrowContinuity = new();
    private static readonly ContinuityStreamOwner<GetBorrowBatchesResponse> BorrowBatchContinuity = new();
    private static readonly ContinuityStreamOwner<GetReadersResponse> ReaderContinuity = new();

    public override async Task GetBooks(GetRequest request, IServerStreamWriter<GetBooksResponse> responseStream,
        ServerCallContext context)
    {
        await using var cmd = db.DataSource.CreateCommand("select * from books");
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var book = new Book
            {
                Id = reader.GetGuid(0).ToString(),
                Name = reader.GetString(1),
                Author = reader.GetString(2),
                Isbn = reader.GetString(3),
                AvatarUri = reader.GetString(4),
                Stock = (uint)reader.GetInt32(5)
            };
            await responseStream.WriteAsync(new GetBooksResponse
            {
                End = false,
                Id = book.Id,
                Book = book
            });
        }

        await responseStream.WriteAsync(new GetBooksResponse
        {
            End = true
        });
        await BookContinuity.Hold(responseStream);
    }

    private void _putBookIntoParameters(DbCommand cmd, Book book)
    {
        cmd.Parameters.Add(db.CreateParameter(Guid.Parse(book.Id)));
        cmd.Parameters.Add(db.CreateParameter(book.Name));
        cmd.Parameters.Add(db.CreateParameter(book.Author));
        cmd.Parameters.Add(db.CreateParameter(book.Isbn));
        cmd.Parameters.Add(db.CreateParameter(book.AvatarUri));
        cmd.Parameters.Add(db.CreateParameter((long)book.Stock));
    }

    public override async Task<UpdateResponse> AddBook(AddRequest request, ServerCallContext context)
    {
        if (await authenticator.GetUserIdFromToken(request.Token.ToByteArray()) == null)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectForbidden
            };
        }

        var book = request.Book;
        if (book == null)
        {
            throw new ArgumentNullException("book");
        }

        await using var cmd = db.DataSource.CreateCommand(
            """
            insert into books
            values ($1, $2, $3, $4, $5, $6)
            """);
        _putBookIntoParameters(cmd, book);
        await cmd.ExecuteNonQueryAsync();

        await Task.WhenAll(
            BookContinuity.SuspendedWriters.Select(writer =>
                writer.WriteAsync(new GetBooksResponse
                {
                    End = false,
                    Id = book.Id,
                    Book = book
                })
            )
        );

        return new UpdateResponse
        {
            Effect = UpdateEffect.EffectOk
        };
    }

    public override async Task<UpdateResponse> UpdateBook(UpdateRequest request, ServerCallContext context)
    {
        if (await authenticator.GetUserIdFromToken(request.Token.ToByteArray()) == null)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectForbidden
            };
        }

        var book = request.Book;
        if (book == null)
        {
            throw new ArgumentNullException("book");
        }

        await using var cmd = db.DataSource.CreateCommand(
            """
            update books set (name, author, isbn, avatar_uri, stock) = ($2, $3, $4, $5, $6) where id = $1
            """
        );
        _putBookIntoParameters(cmd, book);
        var affected = await cmd.ExecuteNonQueryAsync();
        if (affected <= 0)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectNotFound
            };
        }

        await Task.WhenAll(
            BookContinuity.SuspendedWriters.Select(writer =>
                writer.WriteAsync(new GetBooksResponse
                {
                    End = false,
                    Id = book.Id,
                    Book = book
                })
            )
        );

        return new UpdateResponse
        {
            Effect = UpdateEffect.EffectOk
        };
    }

    public override async Task<UpdateResponse> DeleteBook(DeleteRequest request, ServerCallContext context)
    {
        if (await authenticator.GetUserIdFromToken(request.Token.ToByteArray()) == null)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectForbidden
            };
        }

        await using var cmd = db.DataSource.CreateCommand("delete from books where id = $1");
        cmd.Parameters.Add(db.CreateParameter(Guid.Parse(request.Id)));
        var affected = await cmd.ExecuteNonQueryAsync();
        if (affected <= 0)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectNotFound
            };
        }

        foreach (var writer in BookContinuity.SuspendedWriters)
        {
            await writer.WriteAsync(new GetBooksResponse
            {
                End = false,
                Id = request.Id,
                Book = null
            });
        }

        return new UpdateResponse
        {
            Effect = UpdateEffect.EffectOk
        };
    }

    public override async Task GetReaders(GetRequest request, IServerStreamWriter<GetReadersResponse> responseStream,
        ServerCallContext context)
    {
        await using var cmd = db.DataSource.CreateCommand("select * from readers");
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var r = new Reader
            {
                Id = reader.GetGuid(0).ToString(),
                Name = reader.GetString(1),
                AvatarUri = reader.GetString(2),
                Tier = (ReaderTier)reader.GetInt16(3),
                Creditability = reader.GetFloat(4)
            };
            await responseStream.WriteAsync(new GetReadersResponse
            {
                Id = r.Id,
                End = false,
                Reader = r
            });
        }

        await responseStream.WriteAsync(new GetReadersResponse
        {
            End = true
        });
        await ReaderContinuity.Hold(responseStream);
    }

    private void _putReaderIntoParameters(Reader reader, DbCommand cmd)
    {
        cmd.Parameters.Add(db.CreateParameter(Guid.Parse(reader.Id)));
        cmd.Parameters.Add(db.CreateParameter(reader.Name));
        cmd.Parameters.Add(db.CreateParameter(reader.AvatarUri));
        cmd.Parameters.Add(db.CreateParameter((int)reader.Tier));
        cmd.Parameters.Add(db.CreateParameter(reader.Creditability));
    }

    public override async Task<UpdateResponse> UpdateReader(UpdateRequest request, ServerCallContext context)
    {
        if (await authenticator.GetUserIdFromToken(request.Token.ToByteArray()) == null)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectForbidden
            };
        }

        var reader = request.Reader;
        if (reader == null)
        {
            throw new ArgumentNullException("reader");
        }

        await using var cmd = db.DataSource.CreateCommand(
            """
            update readers
            set (name, avatar_uri, tier, creditability) = ($2, $3, $4, $5)
            where id = $1
            """);
        _putReaderIntoParameters(reader, cmd);
        var affected = await cmd.ExecuteNonQueryAsync();
        if (affected <= 0)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectNotFound
            };
        }

        await Task.WhenAll(
            ReaderContinuity.SuspendedWriters.Select(writer =>
                writer.WriteAsync(new GetReadersResponse
                {
                    End = false,
                    Id = reader.Id,
                    Reader = reader
                })
            )
        );

        return new UpdateResponse
        {
            Effect = UpdateEffect.EffectOk
        };
    }

    public override async Task<UpdateResponse> AddReader(AddRequest request, ServerCallContext context)
    {
        if (await authenticator.GetUserIdFromToken(request.Token.ToByteArray()) == null)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectForbidden
            };
        }

        var reader = request.Reader;
        if (reader == null)
        {
            throw new ArgumentNullException("reader");
        }

        await using var cmd = db.DataSource.CreateCommand("insert into readers values ($1, $2, $3, $4, $5)");
        _putReaderIntoParameters(reader, cmd);
        await cmd.ExecuteNonQueryAsync();

        await Task.WhenAll(
            ReaderContinuity.SuspendedWriters.Select(writer =>
                writer.WriteAsync(new GetReadersResponse
                {
                    End = false,
                    Id = reader.Id,
                    Reader = reader
                }))
        );

        return new UpdateResponse
        {
            Effect = UpdateEffect.EffectOk
        };
    }

    public override async Task<UpdateResponse> DeleteReader(DeleteRequest request, ServerCallContext context)
    {
        if (await authenticator.GetUserIdFromToken(request.Token.ToByteArray()) == null)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectForbidden
            };
        }

        await using var cmd = db.DataSource.CreateCommand("delete from readers where id = $1");
        cmd.Parameters.Add(db.CreateParameter(Guid.Parse(request.Id)));
        var affected = await cmd.ExecuteNonQueryAsync();
        if (affected <= 0)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectNotFound
            };
        }

        await Task.WhenAll(ReaderContinuity.SuspendedWriters.Select(writer =>
            writer.WriteAsync(new GetReadersResponse
            {
                End = false,
                Id = request.Id,
                Reader = null
            })
        ));

        return new UpdateResponse
        {
            Effect = UpdateEffect.EffectOk
        };
    }

    public override async Task GetBorrows(GetRequest request, IServerStreamWriter<GetBorrowsResponse> responseStream,
        ServerCallContext context)
    {
        await using var cmd = db.DataSource.CreateCommand("select * from borrows");
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var borrow = new Borrow
            {
                Id = reader.GetGuid(0).ToString(),
                ReaderId = reader.GetGuid(1).ToString(),
                BookId = reader.GetGuid(2).ToString(),
                Time = Timestamp.FromDateTime(reader.GetDateTime(3).ToUniversalTime()),
                DueTime = Timestamp.FromDateTime(reader.GetDateTime(4).ToUniversalTime()),
            };
            if (!await reader.IsDBNullAsync(5))
            {
                borrow.ReturnTime = Timestamp.FromDateTime(reader.GetDateTime(5).ToUniversalTime());
            }

            await responseStream.WriteAsync(new GetBorrowsResponse
            {
                Id = borrow.Id,
                End = false,
                Borrow = borrow
            });
        }

        await responseStream.WriteAsync(new GetBorrowsResponse
        {
            End = true
        });
        await BorrowContinuity.Hold(responseStream);
    }

    private void _putBorrowIntoParameters(Borrow borrow, DbCommand cmd)
    {
        cmd.Parameters.Add(db.CreateParameter(Guid.Parse(borrow.Id)));
        cmd.Parameters.Add(db.CreateParameter(Guid.Parse(borrow.ReaderId)));
        cmd.Parameters.Add(db.CreateParameter(Guid.Parse(borrow.BookId)));
        cmd.Parameters.Add(db.CreateParameter(borrow.Time.ToDateTime()));
        cmd.Parameters.Add(db.CreateParameter(borrow.DueTime.ToDateTime()));
        cmd.Parameters.Add(borrow.ReturnTime != null
            ? db.CreateParameter(borrow.ReturnTime.ToDateTime())
            : db.CreateParameter(DBNull.Value, "timestamp"));
    }

    public override async Task<UpdateResponse> UpdateBorrow(UpdateRequest request, ServerCallContext context)
    {
        if (await authenticator.GetUserIdFromToken(request.Token.ToByteArray()) == null)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectForbidden
            };
        }

        var borrow = request.Borrow;
        if (borrow == null)
        {
            throw new ArgumentNullException("borrow");
        }

        await using var cmd = db.DataSource.CreateCommand(
            """
            update borrows
            set (reader_id, book_id, borrow_time, return_time) = ($2, $3, $4, $5)
            where id = $1
            """);
        _putBorrowIntoParameters(borrow, cmd);
        var affected = await cmd.ExecuteNonQueryAsync();
        if (affected <= 0)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectNotFound
            };
        }

        await Task.WhenAll(
            BorrowContinuity.SuspendedWriters.Select(writer =>
                writer.WriteAsync(new GetBorrowsResponse
                {
                    End = false,
                    Id = borrow.Id,
                    Borrow = borrow
                })
            )
        );

        return new UpdateResponse
        {
            Effect = UpdateEffect.EffectOk
        };
    }

    public override async Task<UpdateResponse> AddBorrow(AddRequest request, ServerCallContext context)
    {
        if (await authenticator.GetUserIdFromToken(request.Token.ToByteArray()) == null)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectForbidden
            };
        }

        var borrow = request.Borrow;
        if (borrow == null)
        {
            throw new ArgumentNullException("borrow");
        }

        await using var cmd = db.DataSource.CreateCommand(
            """
            insert into borrows 
            values ($1, $2, $3, $4, $5, $6)
            """
        );
        _putBorrowIntoParameters(borrow, cmd);
        var affected = await cmd.ExecuteNonQueryAsync();
        if (affected <= 0)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectNotFound
            };
        }

        await Task.WhenAll(
            BorrowContinuity.SuspendedWriters.Select(writer =>
                writer.WriteAsync(new GetBorrowsResponse
                {
                    End = false,
                    Id = borrow.Id,
                    Borrow = borrow
                })
            )
        );

        return new UpdateResponse
        {
            Effect = UpdateEffect.EffectOk
        };
    }

    public override async Task<UpdateResponse> DeleteBorrow(DeleteRequest request, ServerCallContext context)
    {
        if (await authenticator.GetUserIdFromToken(request.Token.ToByteArray()) == null)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectForbidden
            };
        }

        await using var cmd = db.DataSource.CreateCommand("delete from borrows where id = $1");
        cmd.Parameters.Add(db.CreateParameter(Guid.Parse(request.Id)));
        var affected = await cmd.ExecuteNonQueryAsync();

        if (affected <= 0)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectNotFound
            };
        }

        await Task.WhenAll(
            BorrowContinuity.SuspendedWriters.Select(writer =>
                writer.WriteAsync(new GetBorrowsResponse
                {
                    End = false,
                    Id = request.Id,
                    Borrow = null
                })
            )
        );

        return new UpdateResponse
        {
            Effect = UpdateEffect.EffectOk
        };
    }

    private async Task<Collection<Guid>> _queryBatchedBookIds(Guid batchId)
    {
        await using var queryBooks =
            db.DataSource.CreateCommand("select book_id from book_borrow_batches where batch_id = $1");
        queryBooks.Parameters.Add(db.CreateParameter(batchId));
        await using var bookReader = await queryBooks.ExecuteReaderAsync();
        Collection<Guid> result = [];
        while (await bookReader.ReadAsync())
        {
            result.Add(bookReader.GetGuid(0));
        }

        return result;
    }

    public override async Task GetBorrowBatches(GetRequest request,
        IServerStreamWriter<GetBorrowBatchesResponse> responseStream, ServerCallContext context)
    {
        await using var cmd = db.DataSource.CreateCommand("select * from borrow_batches");
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var batchId = reader.GetGuid(0);
            var batch = new BorrowBatch
            {
                Id = batchId.ToString(),
                ReaderId = reader.GetGuid(1).ToString(),
                Time = Timestamp.FromDateTime(reader.GetDateTime(2).ToUniversalTime()),
                DueTime = Timestamp.FromDateTime(reader.GetDateTime(3).ToUniversalTime())
            };
            if (!await reader.IsDBNullAsync(4))
            {
                batch.ReturnTime = Timestamp.FromDateTime(reader.GetDateTime(4).ToUniversalTime());
            }

            foreach (var id in await _queryBatchedBookIds(batchId))
            {
                batch.BookIds.Add(id.ToString());
            }

            await responseStream.WriteAsync(new GetBorrowBatchesResponse
            {
                Id = batch.Id,
                End = false,
                Batch = batch
            });
        }

        await responseStream.WriteAsync(new GetBorrowBatchesResponse
        {
            End = true
        });
        await BorrowBatchContinuity.Hold(responseStream);
    }

    private void _putBorrowBatchIntoParameters(BorrowBatch batch, DbCommand cmd)
    {
        cmd.Parameters.Add(db.CreateParameter(Guid.Parse(batch.Id)));
        cmd.Parameters.Add(db.CreateParameter(Guid.Parse(batch.ReaderId)));
        cmd.Parameters.Add(db.CreateParameter(batch.Time.ToDateTime()));
        cmd.Parameters.Add(db.CreateParameter(batch.DueTime.ToDateTime()));
        cmd.Parameters.Add(
            batch.ReturnTime != null
                ? db.CreateParameter(batch.ReturnTime.ToDateTime())
                : db.CreateParameter(DBNull.Value, "timestamp"));
    }

    public override async Task<UpdateResponse> UpdateBorrowBatch(UpdateRequest request, ServerCallContext context)
    {
        if (await authenticator.GetUserIdFromToken(request.Token.ToByteArray()) == null)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectForbidden
            };
        }

        var batch = request.Batch;
        if (batch == null)
        {
            throw new ArgumentNullException("batch");
        }

        var batchId = Guid.Parse(request.Id);
        var originalIds = await _queryBatchedBookIds(batchId);
        if (!batch.BookIds.ToHashSet().SetEquals(originalIds.Select(guid => guid.ToString()).AsEnumerable()))
        {
            // update the relational table
            await using var removal =
                db.DataSource.CreateCommand("delete from book_borrow_batches where batch_id = $1");
            removal.Parameters.Add(db.CreateParameter(batchId));
            await removal.ExecuteNonQueryAsync();

            await using var construct = db.DataSource.CreateBatch();
            foreach (var id in batch.BookIds)
            {
                var item = construct.CreateBatchCommand();
                item.CommandText =
                    """
                        insert into book_borrow_batches
                        values($1, $2)
                    """;
                item.Parameters.Add(db.CreateParameter(batchId));
                item.Parameters.Add(db.CreateParameter(Guid.Parse(id)));
                construct.BatchCommands.Add(item);
            }

            await construct.ExecuteNonQueryAsync();
        }

        await using var cmd =
            db.DataSource.CreateCommand(
                """
                update borrow_batches
                set (reader_id, borrow_time, return_time) = ($2, $3, $4)
                where id = $1
                """);
        _putBorrowBatchIntoParameters(batch, cmd);

        var affected = await cmd.ExecuteNonQueryAsync();
        if (affected <= 0)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectNotFound
            };
        }

        await Task.WhenAll(
            BorrowBatchContinuity.SuspendedWriters.Select(writer =>
                writer.WriteAsync(new GetBorrowBatchesResponse
                {
                    End = false,
                    Id = request.Id,
                    Batch = batch
                })
            )
        );

        return new UpdateResponse
        {
            Effect = UpdateEffect.EffectOk
        };
    }

    public override async Task<UpdateResponse> AddBorrowBatch(AddRequest request, ServerCallContext context)
    {
        if (await authenticator.GetUserIdFromToken(request.Token.ToByteArray()) == null)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectForbidden
            };
        }

        var batch = request.Batch;
        if (batch == null)
        {
            throw new ArgumentNullException("batch");
        }

        await using var cmd = db.DataSource.CreateCommand(
            """
            insert into borrow_batches
            values ($1, $2, $3, $4, $5)
            """);
        _putBorrowBatchIntoParameters(batch, cmd);
        await cmd.ExecuteNonQueryAsync();

        var batchId = Guid.Parse(batch.Id);
        await using var relational = db.DataSource.CreateBatch();
        foreach (var bookId in batch.BookIds)
        {
            var makeOne = relational.CreateBatchCommand();
            makeOne.CommandText =
                """
                insert into book_borrow_batches
                values ($1, $2)
                """;
            makeOne.Parameters.Add(db.CreateParameter(batchId));
            makeOne.Parameters.Add(db.CreateParameter(Guid.Parse(bookId)));
            relational.BatchCommands.Add(makeOne);
        }

        await relational.ExecuteNonQueryAsync();

        await Task.WhenAll(
            BorrowBatchContinuity.SuspendedWriters.Select(writer =>
                writer.WriteAsync(
                    new GetBorrowBatchesResponse
                    {
                        End = false,
                        Id = batch.Id,
                        Batch = batch
                    }
                )
            )
        );

        return new UpdateResponse
        {
            Effect = UpdateEffect.EffectOk
        };
    }

    public override async Task<UpdateResponse> DeleteBorrowBatch(DeleteRequest request, ServerCallContext context)
    {
        if (await authenticator.GetUserIdFromToken(request.Token.ToByteArray()) == null)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectForbidden
            };
        }

        await using var cmd = db.DataSource.CreateCommand("delete from borrow_batches where id = $1");
        cmd.Parameters.Add(db.CreateParameter(Guid.Parse(request.Id)));

        var affected = await cmd.ExecuteNonQueryAsync();
        if (affected <= 0)
        {
            return new UpdateResponse
            {
                Effect = UpdateEffect.EffectNotFound
            };
        }

        await Task.WhenAll(
            BorrowBatchContinuity.SuspendedWriters.Select(writer =>
                writer.WriteAsync(new GetBorrowBatchesResponse
                {
                    End = false,
                    Id = request.Id,
                    Batch = null
                })
            )
        );

        return new UpdateResponse
        {
            Effect = UpdateEffect.EffectOk
        };
    }
}