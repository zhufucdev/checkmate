using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Sqlmaster.Protobuf;

namespace checkmate.Services;

public class LibraryService(IDatabaseService databaseService) : Library.LibraryBase
{
    private readonly IDatabaseService _db = databaseService;

    public override async Task GetBooks(GetRequest request, IServerStreamWriter<GetBooksResponse> responseStream,
        ServerCallContext context)
    {
        await using var cmd = _db.DataSource.CreateCommand("select * from books");
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
                Ok = true,
                Id = book.Id,
                Book = book
            });
        }
    }

    public override async Task<UpdateResponse> AddBook(AddRequest request, ServerCallContext context)
    {
        return await base.AddBook(request, context);
    }

    public override async Task<UpdateResponse> UpdateBook(UpdateRequest request, ServerCallContext context)
    {
        return await base.UpdateBook(request, context);
    }

    public override async Task<UpdateResponse> DeleteBook(DeleteRequest request, ServerCallContext context)
    {
        return await base.DeleteBook(request, context);
    }

    public override async Task GetReaders(GetRequest request, IServerStreamWriter<GetReadersResponse> responseStream,
        ServerCallContext context)
    {
        await using var cmd = _db.DataSource.CreateCommand("select * from readers");
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
                Ok = true,
                Reader = r
            });
        }
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
        await using var cmd = _db.DataSource.CreateCommand("select * from borrows");
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
                Ok = true,
                Borrow = borrow
            });
        }
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
        await using var cmd = _db.DataSource.CreateCommand("select * from borrow_batches");
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
                _db.DataSource.CreateCommand("select book_id from book_borrow_batches where batch_id = $1");
            queryBooks.Parameters.Add(_db.CreateParameter(batchId));
            await using var bookReader = await queryBooks.ExecuteReaderAsync();
            while (await bookReader.ReadAsync())
            {
                batch.BookIds.Add(bookReader.GetGuid(0).ToString());
            }

            await responseStream.WriteAsync(new GetBorrowBatchesResponse
            {
                Id = batch.Id,
                Ok = true,
                Batch = batch
            });
        }
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