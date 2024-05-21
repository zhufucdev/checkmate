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

        foreach (var writer in BookContinuity.SuspendedWriters)
        {
            await writer.WriteAsync(new GetBooksResponse
            {
                End = false,
                Id = book.Id,
                Book = book
            });
        }

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
        
        foreach (var writer in BookContinuity.SuspendedWriters)
        {
            await writer.WriteAsync(new GetBooksResponse
            {
                End = false,
                Id = book.Id,
                Book = book
            });
        }

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

    public override async Task<UpdateResponse> UpdateReader(UpdateRequest request, ServerCallContext context)
    {
        return await base.UpdateReader(request, context);
    }

    public override async Task<UpdateResponse> AddReader(AddRequest request, ServerCallContext context)
    {
        return await base.AddReader(request, context);
    }

    public override async Task<UpdateResponse> DeleteReader(DeleteRequest request, ServerCallContext context)
    {
        return await base.DeleteReader(request, context);
    }

    public override async Task GetBorrows(GetRequest request, IServerStreamWriter<GetBorrowsResponse> responseStream,
        ServerCallContext context)
    {
        await using var cmd = db.DataSource.CreateCommand("select * from borrows");
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            Timestamp? returnTime = null;
            if (!await reader.IsDBNullAsync(5))
            {
                returnTime = Timestamp.FromDateTimeOffset(DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5)));
            }

            var borrow = new Borrow
            {
                Id = reader.GetGuid(0).ToString(),
                ReaderId = reader.GetGuid(1).ToString(),
                BookId = reader.GetGuid(2).ToString(),
                Time = Timestamp.FromDateTimeOffset(DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(3))),
                DueTime = Timestamp.FromDateTimeOffset(DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(4))),
                ReturnTime = returnTime
            };
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

    public override async Task<UpdateResponse> UpdateBorrow(UpdateRequest request, ServerCallContext context)
    {
        return await base.UpdateBorrow(request, context);
    }

    public override async Task<UpdateResponse> AddBorrow(AddRequest request, ServerCallContext context)
    {
        return await base.AddBorrow(request, context);
    }

    public override async Task<UpdateResponse> DeleteBorrow(DeleteRequest request, ServerCallContext context)
    {
        return await base.DeleteBorrow(request, context);
    }

    public override async Task GetBorrowBatches(GetRequest request,
        IServerStreamWriter<GetBorrowBatchesResponse> responseStream, ServerCallContext context)
    {
        await using var cmd = db.DataSource.CreateCommand("select * from borrow_batches");
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            Timestamp? returnTime = null;
            if (!await reader.IsDBNullAsync(5))
            {
                returnTime = Timestamp.FromDateTimeOffset(DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5)));
            }

            var batchId = reader.GetGuid(0);
            var batch = new BorrowBatch
            {
                Id = batchId.ToString(),
                ReaderId = reader.GetGuid(1).ToString(),
                Time = Timestamp.FromDateTimeOffset(DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(2))),
                ReturnTime = returnTime
            };
            await using var queryBooks =
                db.DataSource.CreateCommand("select book_id from book_borrow_batches where batch_id = $1");
            queryBooks.Parameters.Add(db.CreateParameter(batchId));
            await using var bookReader = await queryBooks.ExecuteReaderAsync();
            while (await bookReader.ReadAsync())
            {
                batch.BookIds.Add(bookReader.GetGuid(0).ToString());
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

    public override async Task<UpdateResponse> UpdateBorrowBatch(UpdateRequest request, ServerCallContext context)
    {
        return await base.UpdateBorrowBatch(request, context);
    }

    public override async Task<UpdateResponse> AddBorrowBatch(AddRequest request, ServerCallContext context)
    {
        return await base.AddBorrowBatch(request, context);
    }

    public override async Task<UpdateResponse> DeleteBorrowBatch(DeleteRequest request, ServerCallContext context)
    {
        return await base.DeleteBorrowBatch(request, context);
    }
}