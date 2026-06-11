using System.Text.Json;
using AgentHarness.Ollama;

namespace AgentHarness.Tools;

public sealed class ToolRegistry
{
    private readonly Dictionary<string, IAgentTool> _toolsByName;

    public ToolRegistry(IEnumerable<IAgentTool> tools)
    {
        _toolsByName = tools.ToDictionary(tool => tool.Name, StringComparer.Ordinal);
    }

    public IReadOnlyList<OllamaToolDefinition> GetToolDefinitions() =>
        _toolsByName.Values
            .Select(tool => new OllamaToolDefinition(tool.Name, tool.Description, tool.ParametersSchema))
            .ToList();

    public async Task<string> InvokeAsync(string toolName, JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!_toolsByName.TryGetValue(toolName, out var tool))
        {
            throw new InvalidOperationException($"Model requested unknown tool '{toolName}'.");
        }

        return await tool.InvokeAsync(arguments, cancellationToken);
    }
}
