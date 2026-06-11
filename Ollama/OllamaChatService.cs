using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentHarness.Ollama;

public sealed class OllamaChatService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    public OllamaChatService(string baseUrl, string model)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _model = model;
    }

    public async Task<OllamaChatResult> ChatAsync(
        IReadOnlyList<OllamaMessage> messages,
        IReadOnlyList<OllamaToolDefinition> tools,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new OllamaChatRequest(_model, messages, tools);
            using var response = await _httpClient.PostAsJsonAsync("/api/chat", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var endpoint = new Uri(_httpClient.BaseAddress!, "/api/chat");
                throw new InvalidOperationException(
                    $"Ollama returned {(int)response.StatusCode} ({response.StatusCode}) for '{endpoint}'. Body: {responseBody}");
            }

            var payload = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: cancellationToken);
            if (payload?.Message is null)
            {
                throw new InvalidOperationException("Ollama response did not contain a message.");
            }

            var toolCalls = payload.Message.ToolCalls?
                .Where(call => call.Function?.Name is not null && call.Function.Arguments.HasValue)
                .Select(call => new OllamaToolCall(
                    call.Function!.Name!,
                    call.Function.Arguments!.Value.Clone(),
                    call.Function.Index?.ToString()))
                .ToList() ?? [];

            return new OllamaChatResult(payload.Message.ToDomain(), payload.Message.Content, toolCalls);
        }
        catch (HttpRequestException exception)
        {
            var endpoint = new Uri(_httpClient.BaseAddress!, "/api/chat");
            var status = exception.StatusCode?.ToString() ?? "unknown";
            throw new InvalidOperationException(
                $"Failed to call Ollama endpoint '{endpoint}'. HTTP status: {status}. {exception.Message}",
                exception);
        }
    }

    private sealed record OllamaChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<OllamaMessage> Messages,
        [property: JsonPropertyName("tools")] IReadOnlyList<OllamaToolDefinition> Tools,
        [property: JsonPropertyName("stream")] bool Stream = false);

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaResponseMessage? Message { get; init; }
    }

    private sealed class OllamaResponseMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; init; } = string.Empty;

        [JsonPropertyName("content")]
        public string? Content { get; init; }

        [JsonPropertyName("tool_calls")]
        public List<OllamaResponseToolCall>? ToolCalls { get; init; }

        public OllamaMessage ToDomain() =>
            new(Role, Content, null, ToolCalls?.Select(call => call.ToDomain()).ToList());
    }

    private sealed class OllamaResponseToolCall
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "function";

        [JsonPropertyName("function")]
        public OllamaResponseFunctionCall? Function { get; init; }

        public OllamaFunctionToolCall ToDomain() =>
            new(Type, new OllamaFunctionCall(
                Function?.Name ?? string.Empty,
                Function?.Arguments ?? default,
                Function?.Index));
    }

    private sealed class OllamaResponseFunctionCall
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("arguments")]
        public JsonElement? Arguments { get; init; }

        [JsonPropertyName("index")]
        public int? Index { get; init; }
    }
}

public sealed record OllamaMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("tool_name")] string? ToolName = null,
    [property: JsonPropertyName("tool_calls")] IReadOnlyList<OllamaFunctionToolCall>? ToolCalls = null);

public sealed record OllamaFunctionToolCall(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("function")] OllamaFunctionCall Function);

public sealed record OllamaFunctionCall(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("arguments")] JsonElement Arguments,
    [property: JsonPropertyName("index")] int? Index = null);

public sealed record OllamaToolDefinition(
    string Name,
    string Description,
    JsonElement ParametersSchema)
{
    [JsonPropertyName("type")]
    public string Type => "function";

    [JsonPropertyName("function")]
    public OllamaToolFunction Function => new(Name, Description, ParametersSchema);
}

public sealed record OllamaToolFunction(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("parameters")] JsonElement Parameters);

public sealed record OllamaToolCall(string Name, JsonElement Arguments, string? Id);

public sealed record OllamaChatResult(
    OllamaMessage AssistantMessage,
    string? Text,
    IReadOnlyList<OllamaToolCall> ToolCalls);
