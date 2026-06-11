using System.Text.Json;

namespace AgentHarness.Tools;

public sealed class GetCurrentTimeTool : IAgentTool
{
    private static readonly JsonElement EmptySchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {}
        }
        """);

    public string Name => "get_current_time";

    public string Description => "Returns the current UTC date and time in ISO-8601 format.";

    public JsonElement ParametersSchema => EmptySchema;

    public Task<string> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(DateTimeOffset.UtcNow.ToString("O"));
    }
}
