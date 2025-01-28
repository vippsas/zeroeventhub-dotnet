using System.Text.Json;
using Shouldly;
using ZeroEventHubClient.Models;

namespace ZeroEventHubClientTests;

public class EventReceiverTests
{
    private struct EventData
    {
        public EventData(string value)
        {
            Value = value;
        }

        public string Value { get; set; }
    }

    [Fact]
    public void Event_AddMultiple_ReturnsEvents()
    {
        var testData = Guid.NewGuid().ToString();

        var eventReceiver = new EventReceiver<EventData>();

        var headers = new Dictionary<string, string> { { "test", "test" } };
        var partitionIds = new[] { 0, 1, 2 };

        foreach (var id in partitionIds)
        {
            eventReceiver.Event(id, headers, JsonSerializer.SerializeToElement(new EventData(testData)));
        }

        var events = eventReceiver.Events.ToList();
        events.Count.ShouldBe(3);

        foreach (var id in partitionIds)
        {
            events[id].PartitionId.ShouldBe(id);
            events[id].Headers.ShouldBeSameAs(headers);
            events[id].Data.Value.ShouldBe(testData);
        }
    }

    [Fact]
    public void Checkpoint_AddMultiple_ReturnsLatestCheckpoint()
    {
        var eventReceiver = new EventReceiver<EventData>();

        eventReceiver.Checkpoint(0, "first0");
        eventReceiver.Checkpoint(0, "second0");
        eventReceiver.Checkpoint(0, "latest0");
        eventReceiver.Checkpoint(1, "first1");
        eventReceiver.Checkpoint(1, "second1");
        eventReceiver.Checkpoint(1, "latest1");

        eventReceiver.Checkpoints.Count.ShouldBe(6);

        var latestCheckpoints = eventReceiver.LatestCheckpoints;
        latestCheckpoints.ShouldBe(new Cursor[]{new(0, "latest0"), new(1, "latest1")});
    }
}
