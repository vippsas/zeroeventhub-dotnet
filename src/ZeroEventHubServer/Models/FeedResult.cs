namespace ZeroEventHubServer.Models;

public record FeedResult<TEvent, TCursor>(IEnumerable<TEvent> Events, TCursor Cursor);
