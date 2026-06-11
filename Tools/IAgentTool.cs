using System.Text.Json;

namespace AgentHarness.Tools;

public interface IAgentTool
{
    string Name { get; }

    string Description { get; }

    JsonElement ParametersSchema { get; }

    Task<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken);
}
