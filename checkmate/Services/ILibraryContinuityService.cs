using Sqlmaster.Protobuf;

namespace checkmate.Services;

public interface ILibraryContinuityService : IDisposable
{
    ContinuityStreamOwner<GetBooksResponse> Book { get; }
    ContinuityStreamOwner<GetBorrowsResponse> Borrow { get; }
    ContinuityStreamOwner<GetBorrowBatchesResponse> BorrowBatch { get; }
    ContinuityStreamOwner<GetReadersResponse> Reader { get; }
    ContinuityStreamOwner<GetUsersResponse> User { get; }
}