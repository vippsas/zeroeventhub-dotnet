namespace ZeroEventHubServer.Models;

public record FeedResponse<T>(List<FeedEntry> FeedEntries);
