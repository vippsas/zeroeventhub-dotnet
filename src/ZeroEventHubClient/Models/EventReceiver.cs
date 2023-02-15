using System.Text.Json;
using ZeroEventHubClient.Exceptions;

namespace ZeroEventHubClient.Models;

public class EventReceiver<T> : IEventReceiver
{
    private readonly List<Event<T>> _events = new();
    private readonly List<Cursor> _checkpoints = new();
    private readonly Dictionary<int, Cursor> _latestCheckpoints = new();

    public IReadOnlyCollection<Event<T>> Events => _events;
    public IReadOnlyCollection<Cursor> Checkpoints => _checkpoints;
    public IReadOnlyCollection<Cursor> LatestCheckpoints => _latestCheckpoints.Values;

    public void Event(int partitionId, IReadOnlyDictionary<string, string>? headers, JsonElement data)
    {
        var deserializedData = Deserialize(data);
        _events.Add(new Event<T>(partitionId, headers, deserializedData));
    }

    protected virtual T Deserialize(JsonElement data)
    {
        T? result;
        try
        {
            result = data.Deserialize<T>();
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or InvalidOperationException)
        {
            throw new MalformedResponseException("Failed to deserialize Event data", e);
        }

        return result ?? throw new MalformedResponseException("Event data is null");
    }

    public void Checkpoint(int partitionId, string cursor)
    {
        var newCursor = new Cursor(partitionId, cursor);
        _checkpoints.Add(newCursor);
        _latestCheckpoints[partitionId] = newCursor;
    }
}
