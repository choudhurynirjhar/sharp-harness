using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AgentHarness.Tools;

public sealed class HttpRequestTool : IAgentTool
{
    private const int MaxResponseBodyLength = 32_000;
    private static readonly HashSet<string> AllowedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "POST", "PUT", "DELETE"
    };

    private static readonly JsonElement Schema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "required": ["method", "url"],
          "properties": {
            "method": {
              "type": "string",
              "enum": ["GET", "POST", "PUT", "DELETE"],
              "description": "HTTP method to use."
            },
            "url": {
              "type": "string",
              "description": "Absolute HTTP or HTTPS URL to request."
            },
            "headers": {
              "type": "object",
              "additionalProperties": { "type": "string" },
              "description": "Optional HTTP request headers."
            },
            "body": {
              "type": "string",
              "description": "Optional request body for POST and PUT requests."
            }
          }
        }
        """);

    private readonly HttpClient _httpClient;

    public HttpRequestTool(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public string Name => "http_request";

    public string Description =>
        "Sends an HTTP request using GET, POST, PUT, or DELETE and returns status code and response body.";

    public JsonElement ParametersSchema => Schema;

    public async Task<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!arguments.TryGetProperty("method", out var methodElement) ||
            methodElement.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException("Missing required argument 'method'.");
        }

        if (!arguments.TryGetProperty("url", out var urlElement) ||
            urlElement.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException("Missing required argument 'url'.");
        }

        var method = methodElement.GetString()!;
        if (!AllowedMethods.Contains(method))
        {
            throw new ArgumentException($"Unsupported HTTP method '{method}'.");
        }

        if (!Uri.TryCreate(urlElement.GetString(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("Argument 'url' must be an absolute http or https URL.");
        }

        using var request = new HttpRequestMessage(new HttpMethod(method.ToUpperInvariant()), uri);

        if (arguments.TryGetProperty("headers", out var headersElement) &&
            headersElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var header in headersElement.EnumerateObject())
            {
                if (!request.Headers.TryAddWithoutValidation(header.Name, header.Value.GetString()))
                {
                    request.Content ??= new StringContent(string.Empty);
                    request.Content.Headers.TryAddWithoutValidation(header.Name, header.Value.GetString());
                }
            }
        }

        if (arguments.TryGetProperty("body", out var bodyElement) &&
            bodyElement.ValueKind == JsonValueKind.String &&
            method is "POST" or "PUT")
        {
            var body = bodyElement.GetString() ?? string.Empty;
            request.Content = new StringContent(body, Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var isTruncated = responseBody.Length > MaxResponseBodyLength;
        if (isTruncated)
        {
            responseBody = responseBody[..MaxResponseBodyLength];
        }

        var result = new
        {
            status_code = (int)response.StatusCode,
            status = response.StatusCode.ToString(),
            content_type = response.Content.Headers.ContentType?.ToString(),
            body = responseBody,
            truncated = isTruncated
        };

        return JsonSerializer.Serialize(result);
    }
}
