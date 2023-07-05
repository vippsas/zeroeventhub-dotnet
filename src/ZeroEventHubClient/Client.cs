using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Web;
using Microsoft.VisualBasic.FileIO;
using ZeroEventHubClient.Exceptions;
using ZeroEventHubClient.Models;

namespace ZeroEventHubClient;

[SuppressMessage("ReSharper", "InvalidXmlDocComment")]
public class Client
{
    private readonly HttpClient _httpClient;
    private readonly Uri _uri;
    private readonly int _partitionCount;
    private readonly Func<HttpRequestMessage, Task> _requestCallback;
    private const int DefaultPageSizeHint = 0;

    /// <summary>
    /// Creates a new instance of <see cref="Client"/>
    /// </summary>
    /// <param name="url">Full path to the server.</param>
    /// <param name="partitionCount">The number of partitions the ZeroEventHub server has.</param>
    /// <param name="requestCallback">Callback that allows modification of the <see cref="HttpRequestMessage"/> to for
    /// instance add authorization headers.</param>
    public Client(string url, int partitionCount, Func<HttpRequestMessage, Task> requestCallback)
        : this(
            url,
            partitionCount,
            requestCallback,
            new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(15) }))
    {

    }

    /// <inheritdoc cref="Client(string,int)"/>
    /// <param name="httpClient">A provided <see cref="HttpClient"/> to use instead of the default one.</param>
    public Client(string url, int partitionCount, Func<HttpRequestMessage, Task> requestCallback, HttpClient httpClient)
    {
        _uri = new Uri(url);
        _partitionCount = partitionCount;
        _requestCallback = requestCallback;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Fetch events from the server
    /// </summary>
    /// <param name="cursors">A sequence of cursors to be used in the request.</param>
    /// <param name="pageSizeHint">An hint for the page size of the response.
    /// Set to 0 if the server should decide the size returned</param>
    /// <param name="eventReceiver">An event receiver to handle the received events.</param>
    /// <exception cref="ArgumentException">If cursors are missing.</exception>
    /// <exception cref="MalformedResponseException">If returned response body could not be parsed.</exception>
    /// <exception cref="HttpRequestException">If response status code does not indicate success.</exception>
    public async Task FetchEvents(
        IReadOnlyCollection<Cursor> cursors,
        int pageSizeHint,
        IEventReceiver eventReceiver,
        CancellationToken cancellationToken = default)
    {
        await FetchEvents(cursors, pageSizeHint, eventReceiver, new List<string>(), cancellationToken);
    }

    /// <summary>
    /// Fetch events from the server
    /// </summary>
    /// <param name="cursor">A single cursor to be used in the request.</param>
    /// <param name="pageSizeHint">A hint for the page size of the response.
    /// Set to 0 if the server should decide the size returned</param>
    /// <param name="eventReceiver">An event receiver to handle the received events.</param>
    /// <exception cref="ArgumentException">If cursors are missing.</exception>
    /// <exception cref="MalformedResponseException">If returned response body could not be parsed.</exception>
    /// <exception cref="HttpRequestException">If response status code does not indicate success.</exception>
    public async Task FetchEvents(
        Cursor cursor,
        int pageSizeHint,
        IEventReceiver eventReceiver,
        CancellationToken cancellationToken = default)
    {
        await FetchEvents(new[] { cursor }, pageSizeHint, eventReceiver, new List<string>(), cancellationToken);
    }

    /// <inheritdoc cref="FetchEvents(System.Collections.Generic.IReadOnlyCollection{ZeroEventHubClient.Models.Cursor},int,ZeroEventHubClient.Models.IEventReceiver)"/>
    /// <param name="headers">An optional sequence containing event headers desired in the response.</param>
    public async Task FetchEvents(
        IReadOnlyCollection<Cursor> cursors,
        int pageSizeHint,
        IEventReceiver eventReceiver,
        IReadOnlyCollection<string> headers,
        CancellationToken cancellationToken = default)
    {
        if (!cursors.Any())
        {
            throw new ArgumentException("Need at least one cursor", nameof(cursors));
        }

        var requestUrl = new UriBuilder(_uri);
        var query = HttpUtility.ParseQueryString(_uri.Query);

        query.Set("n", _partitionCount.ToString());

        foreach (var cursor in cursors)
        {
            query.Set($"cursor{cursor.PartitionId}", cursor.Value);
        }

        if (pageSizeHint != DefaultPageSizeHint)
        {
            query.Set("pagesizehint", pageSizeHint.ToString());
        }

        if (headers.Any())
        {
            query.Set("headers", string.Join(",", headers));
        }

        requestUrl.Query = query.ToString();
        using var message = new HttpRequestMessage(HttpMethod.Get, requestUrl.Uri.AbsoluteUri);

        await _requestCallback.Invoke(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

        IEnumerable<CheckpointOrEvent> responseData;
        try
        {
            responseData = responseString.Split('\n')
                .Where(line => !string.IsNullOrEmpty(line))
                .Select(line => JsonSerializer.Deserialize<CheckpointOrEvent>(line))
                .OfType<CheckpointOrEvent>();
        }
        catch (JsonException e)
        {
            throw new MalformedLineException("Could not deserialize body", e);
        }

        foreach (var eventOrCheckpoint in responseData)
        {
            if (string.IsNullOrEmpty(eventOrCheckpoint.Cursor))
            {
                if (eventOrCheckpoint.Data == null) throw new MalformedResponseException("Cursor and data are both empty");
                eventReceiver.Event(eventOrCheckpoint.PartitionId, eventOrCheckpoint.Headers, eventOrCheckpoint.Data.Value);
            }
            else
            {
                eventReceiver.Checkpoint(eventOrCheckpoint.PartitionId, eventOrCheckpoint.Cursor);
            }
        }
    }
}
