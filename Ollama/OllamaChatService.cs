using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentHarness.Ollama;

public sealed class OllamaChatService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<OllamaChatService> _logger;

    public OllamaChatService(string baseUrl, string model, ILogger<OllamaChatService>? logger = null)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _model = model;
        _logger = logger ?? NullLogger<OllamaChatService>.Instance;
    }

    public async Task<OllamaChatResult> ChatAsync(
        IReadOnlyList<OllamaMessage> messages,
        IReadOnlyList<OllamaToolDefinition> tools,
        CancellationToken cancellationToken)
    {
        try
        {
            LogModelRequest(messages, tools);

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

            var usage = payload.ToUsage();
            LogModelResponse(usage, payload.Message.Content, toolCalls);

            return new OllamaChatResult(payload.Message.ToDomain(), payload.Message.Content, toolCalls, usage);
        }
        catch (HttpRequestException exception)
        {
            var endpoint = new Uri(_httpClient.BaseAddress!, "/api/chat");
            var status = exception.StatusCode?.ToString() ?? "unknown";
            throw new InvalidOperationException(
                $"Failed to call Ollama endpoint '{endpoint}'. HTTP status: {status}. {exception.Message}",
                exception);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Ollama model call failed for {Model}.", _model);
            throw;
        }
    }

    private void LogModelRequest(IReadOnlyList<OllamaMessage> messages, IReadOnlyList<OllamaToolDefinition> tools)
    {
        _logger.LogInformation(
            "Model request -> model={Model} messageCount={MessageCount} tools=[{Tools}]",
            _model,
            messages.Count,
            string.Join(", ", tools.Select(tool => tool.Name)));

        for (var index = 0; index < messages.Count; index++)
        {
            var message = messages[index];
            _logger.LogInformation(
                "Model request message[{Index}] role={Role} toolName={ToolName} content={Content}",
                index,
                message.Role,
                message.ToolName ?? "(none)",
                message.Content ?? "(empty)");

            if (message.ToolCalls is null)
            {
                continue;
            }

            foreach (var toolCall in message.ToolCalls)
            {
                _logger.LogInformation(
                    "Model request message[{Index}] toolCall name={ToolName} arguments={Arguments}",
                    index,
                    toolCall.Function.Name,
                    toolCall.Function.Arguments.GetRawText());
            }
        }
    }

    private void LogModelResponse(OllamaUsage usage, string? content, IReadOnlyList<OllamaToolCall> toolCalls)
    {
        _logger.LogInformation(
            "Model response <- content={Content}",
            string.IsNullOrWhiteSpace(content) ? "(empty)" : content);

        foreach (var toolCall in toolCalls)
        {
            _logger.LogInformation(
                "Model response toolCall name={ToolName} arguments={Arguments}",
                toolCall.Name,
                toolCall.Arguments.GetRawText());
        }

        _logger.LogInformation(
            "Model response usage model={Model} done={Done} doneReason={DoneReason} promptTokens={PromptTokens} completionTokens={CompletionTokens} totalTokens={TotalTokens} tokensPerSecond={TokensPerSecond:F2} totalDurationMs={TotalDurationMs:F0} promptEvalDurationMs={PromptEvalDurationMs:F0} evalDurationMs={EvalDurationMs:F0}",
            usage.Model ?? _model,
            usage.Done,
            usage.DoneReason,
            usage.PromptEvalCount,
            usage.EvalCount,
            usage.TotalTokens,
            usage.TokensPerSecond,
            NanosecondsToMilliseconds(usage.TotalDurationNs),
            NanosecondsToMilliseconds(usage.PromptEvalDurationNs),
            NanosecondsToMilliseconds(usage.EvalDurationNs));
    }

    private static double? NanosecondsToMilliseconds(long? nanoseconds) =>
        nanoseconds is null ? null : nanoseconds.Value / 1_000_000.0;

    private sealed record OllamaChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<OllamaMessage> Messages,
        [property: JsonPropertyName("tools")] IReadOnlyList<OllamaToolDefinition> Tools,
        [property: JsonPropertyName("stream")] bool Stream = false);

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("model")]
        public string? Model { get; init; }

        [JsonPropertyName("message")]
        public OllamaResponseMessage? Message { get; init; }

        [JsonPropertyName("done")]
        public bool Done { get; init; }

        [JsonPropertyName("done_reason")]
        public string? DoneReason { get; init; }

        [JsonPropertyName("total_duration")]
        public long? TotalDurationNs { get; init; }

        [JsonPropertyName("load_duration")]
        public long? LoadDurationNs { get; init; }

        [JsonPropertyName("prompt_eval_count")]
        public int? PromptEvalCount { get; init; }

        [JsonPropertyName("prompt_eval_duration")]
        public long? PromptEvalDurationNs { get; init; }

        [JsonPropertyName("eval_count")]
        public int? EvalCount { get; init; }

        [JsonPropertyName("eval_duration")]
        public long? EvalDurationNs { get; init; }

        public OllamaUsage ToUsage() =>
            new(
                Model,
                Done,
                DoneReason,
                TotalDurationNs,
                LoadDurationNs,
                PromptEvalCount,
                PromptEvalDurationNs,
                EvalCount,
                EvalDurationNs);
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
    IReadOnlyList<OllamaToolCall> ToolCalls,
    OllamaUsage? Usage = null);
