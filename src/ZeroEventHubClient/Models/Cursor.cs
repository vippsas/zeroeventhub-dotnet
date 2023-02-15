namespace ZeroEventHubClient.Models;

public record Cursor(int PartitionId, string Value)
{
    public static Cursor First(int partitionId)
    {
        return new Cursor(partitionId, "_first");
    }

    public static Cursor Last(int partitionId)
    {
        return new Cursor(partitionId, "_last");
    }
}
