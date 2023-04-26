using System.Text.Json.Serialization;

namespace ZeroEventHubServer.Models;

public class FeedEntry
{
    [JsonPropertyName("partition")]
    public int Partition { get; init; } = ZeroEventHubConstants.NullPartition;
}
