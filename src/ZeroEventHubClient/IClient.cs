using ZeroEventHubClient.Models;

namespace ZeroEventHubClient;

public interface IClient
{
    Task FetchEvents(IReadOnlyCollection<Cursor> cursors, int pageSizeHint, IEventReceiver eventReceiver, CancellationToken cancellationToken = default);
    Task FetchEvents(Cursor cursor, int pageSizeHint, IEventReceiver eventReceiver, CancellationToken cancellationToken = default);
    Task FetchEvents(IReadOnlyCollection<Cursor> cursors, int pageSizeHint, IEventReceiver eventReceiver, IReadOnlyCollection<string> headers, CancellationToken cancellationToken = default);
}