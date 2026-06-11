using AgentHarness.Memory;
using AgentHarness.Ollama;
using AgentHarness.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentHarness.Agent;

public sealed class AgentLoop
{
    private const int MaxSteps = 10;
    private readonly OllamaChatService _ollamaChatService;
    private readonly ToolRegistry _toolRegistry;
    private readonly AgentMemory _agentMemory;
    private readonly ILogger<AgentLoop> _logger;

    public AgentLoop(
        OllamaChatService ollamaChatService,
        ToolRegistry toolRegistry,
        AgentMemory agentMemory,
        ILogger<AgentLoop>? logger = null)
    {
        _ollamaChatService = ollamaChatService;
        _toolRegistry = toolRegistry;
        _agentMemory = agentMemory;
        _logger = logger ?? NullLogger<AgentLoop>.Instance;
    }

    public async Task<AgentRunResult> RunAsync(string userMessage, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        _logger.LogInformation("Starting agent loop for user message with length {MessageLength}.", userMessage.Length);

        var messages = new List<OllamaMessage>(
            await _agentMemory.BuildContextMessagesAsync(userMessage, cancellationToken));
        var tools = _toolRegistry.GetToolDefinitions();
        var totalPromptTokens = 0;
        var totalCompletionTokens = 0;

        for (var step = 0; step < MaxSteps; step++)
        {
            _logger.LogInformation("Agent loop step {Step} started.", step + 1);

            var response = await _ollamaChatService.ChatAsync(messages, tools, cancellationToken);
            messages.Add(response.AssistantMessage);

            if (response.Usage is not null)
            {
                totalPromptTokens += response.Usage.PromptEvalCount ?? 0;
                totalCompletionTokens += response.Usage.EvalCount ?? 0;
            }

            if (response.ToolCalls.Count == 0)
            {
                var duration = DateTimeOffset.UtcNow - startedAt;
                var assistantResponse = response.Text ?? string.Empty;

                _logger.LogInformation(
                    "Agent loop completed at step {Step}. TotalPromptTokens={TotalPromptTokens} TotalCompletionTokens={TotalCompletionTokens} TotalTokens={TotalTokens}",
                    step + 1,
                    totalPromptTokens,
                    totalCompletionTokens,
                    totalPromptTokens + totalCompletionTokens);

                await _agentMemory.RecordTurnAsync(userMessage, assistantResponse, cancellationToken);

                return new AgentRunResult(
                    assistantResponse,
                    step + 1,
                    totalPromptTokens,
                    totalCompletionTokens,
                    duration);
            }

            foreach (var call in response.ToolCalls)
            {
                _logger.LogInformation("Executing tool {ToolName}.", call.Name);
                var toolResult = await _toolRegistry.InvokeAsync(call.Name, call.Arguments, cancellationToken);
                messages.Add(new OllamaMessage("tool", toolResult, call.Name));
                _logger.LogInformation("Tool {ToolName} completed with result length {ResultLength}.", call.Name, toolResult.Length);
            }
        }

        throw new InvalidOperationException($"Agent exceeded max steps ({MaxSteps}).");
    }
}
