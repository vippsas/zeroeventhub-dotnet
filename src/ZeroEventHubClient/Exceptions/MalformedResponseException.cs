namespace ZeroEventHubClient.Exceptions;

public class MalformedResponseException : Exception
{
    public MalformedResponseException(string message) : base(message) { }

    public MalformedResponseException(string message, Exception exception) : base(message, exception) { }
}
