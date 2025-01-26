using System.Net;
using System.Text.Json;
using NSubstitute;
using Shouldly;
using ZeroEventHubClient;
using ZeroEventHubClient.Models;

namespace ZeroEventHubClientTests;

public class ClientTests
{
    private readonly IEventReceiver _receiverMock;
    private readonly HttpClient _httpClient;
    private readonly Func<HttpRequestMessage, Task> _processor;
    private bool _called;
    private const string FeedUrl = "https://example.invalid/feed/v1";
    private HttpResponseMessage _httpResponseMessage = new();
    private HttpRequestMessage? _request;

    private class HttpMessageHandlerMock : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handlerDelegate;

        public HttpMessageHandlerMock(Func<HttpRequestMessage, HttpResponseMessage> handlerDelegate)
        {
            _handlerDelegate = handlerDelegate;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handlerDelegate(request));
        }
    }

    public ClientTests()
    {
        // Arrange
        _receiverMock = Substitute.For<IEventReceiver>();
        var httpMessageHandlerMock = new HttpMessageHandlerMock(request =>
        {
            _request = request;
            return _httpResponseMessage;
        });
        _httpClient = new HttpClient(httpMessageHandlerMock);

        _called = false;
        _processor = new Func<HttpRequestMessage, Task>(_ =>
        {
            _called = true;
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task FetchEvents_WithProcessor_CallsProcessor()
    {
        // Act
        var zeroEventHubClient = new Client(FeedUrl, 0, _processor, _httpClient);
        await zeroEventHubClient.FetchEvents(new[] { Cursor.First(0) }, 1, _receiverMock);

        // Assert
        _called.ShouldBe(true);
    }

    [Fact]
    public async Task FetchEvents_CheckpointReturned_CursorCalled()
    {
        // Arrange
        const int PartitionCount = 1;
        const int PartitionId = 0;
        var cursor = Guid.NewGuid().ToString();

        // Act
        _httpResponseMessage = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent($"{{\"partition\": {PartitionId}, \"cursor\": \"{cursor}\"}}",
                    null,
                    "application/x-ndjson"),
        };

        // Act
        var client = new Client(FeedUrl, PartitionCount, _ => Task.CompletedTask, _httpClient);
        await client.FetchEvents(new[] { Cursor.First(PartitionId) }, 1, _receiverMock);

        // Assert
        _receiverMock.Received().Checkpoint(PartitionId, cursor);
    }

    [Fact]
    public async Task FetchEvents_MultipleResponseLines_MultipleCalls()
    {
        // Arrange
        const int PartitionCount = 1;
        const int PartitionId = 0;
        const string Data = "Some data";
        var cursor = Guid.NewGuid().ToString();
        var responseContent =
            $@"{{""partition"": {PartitionId}, ""cursor"": ""{cursor}""}}
            {{""partition"": {PartitionId}, ""headers"": {{""test-header"": ""test""}}, ""data"": {{""data"": ""{Data}""}}}}";

        _httpResponseMessage =
            new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent, null, "application/json"),
            };

        // Act
        var client = new Client(FeedUrl, PartitionCount, _ => Task.CompletedTask, _httpClient);
        await client.FetchEvents(new[] { Cursor.First(PartitionId) }, 1, _receiverMock);

        // Assert
        _receiverMock.Received().Checkpoint(PartitionId, cursor);
        _receiverMock.Received().Event(PartitionId, Arg.Any<Dictionary<string, string>>(), Arg.Any<JsonElement>());
    }

    [Fact]
    public async Task FetchEvents_NormalInput_CorrectRequest()
    {
        // Arrange
        const int PartitionCount = 1;
        const int PartitionId = 0;
        const int PageSize = 1;
        var cursor = Cursor.First(PartitionId);
        var responseContent = $@"{{""partition"": {PartitionId}, ""cursor"": ""{Guid.NewGuid().ToString()}""}}";

        _httpResponseMessage = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(responseContent, null, "application/json")
        };

        // Act
        var client = new Client(FeedUrl, PartitionCount, _ => Task.CompletedTask, _httpClient);
        await client.FetchEvents(new[] { cursor }, PageSize, _receiverMock, new[] { "test1", "test2" });

        // Assert
        _request.ShouldNotBeNull();
        _request.RequestUri.ShouldNotBeNull();
        _request.RequestUri.Query
            .ShouldBe($"?n={PartitionCount}&cursor{PartitionId}=_first&pagesizehint={PageSize}&headers=test1%2ctest2");
    }

    [Fact]
    public async Task FetchEvents_NoCursors_ThrowsException()
    {
        const int PartitionCount = 1;

        // Act
        var client = new Client(FeedUrl, PartitionCount, _ => Task.CompletedTask);
        var action = async () => { await client.FetchEvents(Array.Empty<Cursor>(), 1, _receiverMock); };

        // Assert
        await action.ShouldThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task FetchEvents_ResponseWithNon200Code_ThrowsHttpRequestException()
    {
        // Arrange
        const int PartitionCount = 1;
        const int PartitionId = 0;

        _httpResponseMessage = new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound };

        // Act
        var client = new Client(FeedUrl, PartitionCount, _ => Task.CompletedTask, _httpClient);
        var action = async () =>
        {
            await client.FetchEvents(new[] { Cursor.First(PartitionId) }, 1, _receiverMock);
        };

        // Assert
        await action.ShouldThrowAsync<HttpRequestException>();
    }
}
