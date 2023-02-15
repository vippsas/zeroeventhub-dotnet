namespace ZeroEventHubClient.Models;

public record Event<T>(int PartitionId, IReadOnlyDictionary<string, string>? Headers, T Data);
