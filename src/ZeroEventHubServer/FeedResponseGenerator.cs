using Microsoft.AspNetCore.Mvc;
using ZeroEventHubServer.Models;

namespace ZeroEventHubServer;

public static class FeedResponseGenerator
{
    public static List<FeedEntry> GenerateFeedEntries<TEvent, TCursor>(FeedResult<TEvent, TCursor> feedResult)
    {
        var feedEntries = new List<FeedEntry>();

        foreach (var eventEntry in feedResult.Events)
        {
            var eventFeedResponse = new EventFeedEntry<TEvent>(eventEntry);
            feedEntries.Add(eventFeedResponse);
        }
        var checkpointFeedResponse = new CheckpointFeedEntry<TCursor>(feedResult.Cursor);
        feedEntries.Add(checkpointFeedResponse);

        return feedEntries;
    }

    public static ObjectResult MapToObjectResult(List<FeedEntry> entries)
    {
        return new ObjectResult(
            entries
        )
        { Formatters = { new FeedOutputFormatter() } };
    }
}
