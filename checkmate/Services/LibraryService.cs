using Grpc.Core;
using Sqlmaster.Protobuf;

namespace checkmate.Services;

public class LibraryService(IDatabaseService databaseService) : Library.LibraryBase
{
    private readonly IDatabaseService _db = databaseService;
    public override async Task GetBooks(GetRequest request, IServerStreamWriter<GetBooksResponse> responseStream, ServerCallContext context)
    {
        await base.GetBooks(request, responseStream, context);
    }

    public override async Task<UpdateResponse> AddBook(UpdateRequest request, ServerCallContext context)
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

    public override async Task GetReaders(GetRequest request, IServerStreamWriter<GetReadersResponse> responseStream, ServerCallContext context)
    {
        await base.GetReaders(request, responseStream, context);
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

    public override async Task GetBorrows(GetRequest request, IServerStreamWriter<GetBorrowsResponse> responseStream, ServerCallContext context)
    {
        await base.GetBorrows(request, responseStream, context);
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
}