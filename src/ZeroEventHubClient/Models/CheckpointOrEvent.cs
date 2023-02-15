using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZeroEventHubClient.Models;

public record CheckpointOrEvent(
    [property:JsonPropertyName("partition")]
    int PartitionId,

    [property:JsonPropertyName("cursor")]
    string? Cursor,

    [property:JsonPropertyName("headers")]
    Dictionary<string, string>? Headers,

    [property:JsonPropertyName("data")]
    JsonElement? Data
);
