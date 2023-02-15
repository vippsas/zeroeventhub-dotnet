using System.Text.Json;

namespace ZeroEventHubClient.Models;

public interface IEventReceiver
{
    void Event(int partitionId, IReadOnlyDictionary<string, string>? headers, JsonElement data);
    void Checkpoint(int partitionId, string cursor);
}
