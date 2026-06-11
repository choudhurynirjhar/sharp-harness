using AgentHarness.Ollama;
using AgentHarness.Tools;

namespace AgentHarness.Agent;

public sealed class AgentLoop
{
    private const int MaxSteps = 10;
    private readonly OllamaChatService _ollamaChatService;
    private readonly ToolRegistry _toolRegistry;

    public AgentLoop(OllamaChatService ollamaChatService, ToolRegistry toolRegistry)
    {
        _ollamaChatService = ollamaChatService;
        _toolRegistry = toolRegistry;
    }

    public async Task<string> RunAsync(string userMessage, CancellationToken cancellationToken)
    {
        var messages = new List<OllamaMessage> { new("user", userMessage) };
        var tools = _toolRegistry.GetToolDefinitions();

        for (var step = 0; step < MaxSteps; step++)
        {
            var response = await _ollamaChatService.ChatAsync(messages, tools, cancellationToken);
            messages.Add(response.AssistantMessage);

            if (response.ToolCalls.Count == 0)
            {
                return response.Text ?? string.Empty;
            }

            foreach (var call in response.ToolCalls)
            {
                var toolResult = await _toolRegistry.InvokeAsync(call.Name, call.Arguments, cancellationToken);
                messages.Add(new OllamaMessage("tool", toolResult, call.Name));
            }
        }

        throw new InvalidOperationException($"Agent exceeded max steps ({MaxSteps}).");
    }
}
