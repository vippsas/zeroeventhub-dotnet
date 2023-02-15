using System.Net;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Moq.Protected;
using ZeroEventHubClient;
using ZeroEventHubClient.Models;

namespace ZeroEventHubClientTests;

public class ClientTests
{
    private const string FeedUrl = "https://example.invalid/feed/v1";


    [Fact]
    public async Task FetchEvents_WithProcessor_CallsProcessor()
    {
        // Arrange
        var receiverMock = new Mock<IEventReceiver>();
        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage());
        var httpClient = new HttpClient(httpMessageHandlerMock.Object);

        var called = false;
        var processor = new Func<HttpRequestMessage, Task>(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        var zeroEventHubClient = new Client(FeedUrl, 0, processor, httpClient);
        await zeroEventHubClient.FetchEvents(new[] { Cursor.First(0) }, 1, receiverMock.Object);

        // Assert
        called.Should().Be(true);
    }

    [Fact]
    public async Task FetchEvents_CheckpointReturned_CursorCalled()
    {
        // Arrange
        const int PartitionCount = 1;
        const int PartitionId = 0;
        var cursor = Guid.NewGuid().ToString();

        // Act
        var receiverMock = new Mock<IEventReceiver>();
        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($"{{\"partition\": {PartitionId}, \"cursor\": \"{cursor}\"}}",
                    null,
                    "application/x-ndjson"),
            });
        var httpClient = new HttpClient(httpMessageHandlerMock.Object);

        // Act
        var client = new Client(FeedUrl, PartitionCount, _ => Task.CompletedTask, httpClient);
        await client.FetchEvents(new[] { Cursor.First(PartitionId) }, 1, receiverMock.Object);

        // Assert
        receiverMock.Verify(receiver => receiver.Checkpoint(PartitionId, cursor));
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

        var receiverMock = new Mock<IEventReceiver>();
        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent, null, "application/json"),
            });
        var httpClient = new HttpClient(httpMessageHandlerMock.Object);

        // Act
        var client = new Client(FeedUrl, PartitionCount, _ => Task.CompletedTask, httpClient);
        await client.FetchEvents(new[] { Cursor.First(PartitionId) }, 1, receiverMock.Object);

        // Assert
        receiverMock.Verify(receiver => receiver.Checkpoint(PartitionId, cursor));
        receiverMock.Verify(receiver => receiver.Event(PartitionId,
            new Dictionary<string, string>() { { "test-header", "test" } },
            It.IsAny<JsonElement>()));
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

        HttpRequestMessage? request = null;

        var receiverMock = new Mock<IEventReceiver>();
        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent, null, "application/json"),
            }).Callback<HttpRequestMessage, CancellationToken>((message, _) => { request = message; });

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);

        // Act
        var client = new Client(FeedUrl, PartitionCount, _ => Task.CompletedTask, httpClient);
        await client.FetchEvents(new[] { cursor }, PageSize, receiverMock.Object, new[] { "test1", "test2" });

        // Assert
        request.Should().NotBeNull();
        request!.RequestUri.Should().NotBeNull();
        request.RequestUri!.Query.Should()
            .Be($"?n={PartitionCount}&cursor{PartitionId}=_first&pagesizehint={PageSize}&headers=test1%2ctest2");
    }

    [Fact]
    public async Task FetchEvents_NoCursors_ThrowsException()
    {
        // Arrange
        const int PartitionCount = 1;
        var receiverMock = new Mock<IEventReceiver>();

        // Act
        var client = new Client(FeedUrl, PartitionCount, _ => Task.CompletedTask);
        var action = async () => { await client.FetchEvents(Array.Empty<Cursor>(), 1, receiverMock.Object); };

        // Assert
        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task FetchEvents_ResponseWithNon200Code_ThrowsHttpRequestException()
    {
        // Arrange
        const int PartitionCount = 1;
        const int PartitionId = 0;

        var receiverMock = new Mock<IEventReceiver>();
        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound });

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);

        // Act
        var client = new Client(FeedUrl, PartitionCount, _ => Task.CompletedTask, httpClient);

        var action = async () =>
        {
            await client.FetchEvents(new[] { Cursor.First(PartitionId) }, 1, receiverMock.Object);
        };

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>();
    }
}
