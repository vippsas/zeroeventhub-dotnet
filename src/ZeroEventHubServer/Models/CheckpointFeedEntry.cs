using System.Text.Json.Serialization;

namespace ZeroEventHubServer.Models;

public class CheckpointFeedEntry<T> : FeedEntry
{
    [JsonPropertyName("cursor")]
    public T Cursor { get; init; }

    public CheckpointFeedEntry(T cursor)
    {
        Cursor = cursor;
    }
}
