using Sqlmaster.Protobuf;

namespace checkmate.Services.Impl;

public class LibraryContinuityImpl : ILibraryContinuityService
{
    public void Dispose()
    {
        BorrowBatch.Dispose();
        Borrow.Dispose();
        BorrowBatch.Dispose();
        Reader.Dispose();
    }

    public ContinuityStreamOwner<GetBooksResponse> Book { get; } = new(new GetBooksResponse
    {
        End = true
    });

    public ContinuityStreamOwner<GetBorrowsResponse> Borrow { get; } = new(new GetBorrowsResponse
    {
        End = true
    });

    public ContinuityStreamOwner<GetBorrowBatchesResponse> BorrowBatch { get; } = new(
        new GetBorrowBatchesResponse
        {
            End = true
        });

    public ContinuityStreamOwner<GetReadersResponse> Reader { get; } = new(new GetReadersResponse
    {
        End = true
    });

    public ContinuityStreamOwner<GetUsersResponse> User { get; } = new(new GetUsersResponse
    {
        End = true
    });
}