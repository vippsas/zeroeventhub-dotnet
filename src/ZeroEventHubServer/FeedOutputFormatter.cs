using System.Net;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using ZeroEventHubServer.Models;

namespace ZeroEventHubServer;

public class FeedOutputFormatter : TextOutputFormatter
{
    private static readonly byte[] _lineFeed = { (byte)'\n' };

    public FeedOutputFormatter()
    {
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse(ZeroEventHubConstants.NdJsonContentType));
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse(MediaTypeNames.Application.Json));

        SupportedEncodings.Add(Encoding.UTF8);
    }

    protected override bool CanWriteType(Type? type)
        => typeof(IEnumerable<FeedEntry>).IsAssignableFrom(type);

    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
    {
        var feedElements = (IEnumerable<FeedEntry>)context.Object!;
        var options = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

        var contentType = MediaTypeHeaderValue.Parse(context.ContentType);

        // The feed elements can be either FeedEventModel<E> or FeedCheckpointModel<E>.
        // For both NDJSON and JSON, we therefore cast each element to object. This causes System.Text.Json
        // to serialize it according to its runtime type instead of its declared type.
        if (contentType.MediaType == ZeroEventHubConstants.NdJsonContentType)
        {
            foreach (var element in feedElements)
            {
                await JsonSerializer.SerializeAsync(context.HttpContext.Response.Body, (object)element, options, context.HttpContext.RequestAborted);
                await context.HttpContext.Response.Body.WriteAsync(_lineFeed);
            }
        }
        else if (contentType.MediaType == MediaTypeNames.Application.Json)
        {
            await JsonSerializer.SerializeAsync(context.HttpContext.Response.Body, feedElements.Select(e => (object)e), options,
                context.HttpContext.RequestAborted);
        }
        else
        {
            context.HttpContext.Response.StatusCode = (int)HttpStatusCode.NotAcceptable;
        }
    }

}
