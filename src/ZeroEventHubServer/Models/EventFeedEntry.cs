using System.Text.Json.Serialization;

namespace ZeroEventHubServer.Models;

public class EventFeedEntry<T> : FeedEntry
{
    [JsonPropertyName("data")]
    public T Data { get; init; }

    public EventFeedEntry(T data)
    {
        Data = data;
    }
}
