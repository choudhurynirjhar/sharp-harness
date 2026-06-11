using System.Threading.Channels;
using AgentHarness.Agent;
using AgentHarness.Messaging;
using AgentHarness.Ollama;
using AgentHarness.Tools;
using AgentRuntime = AgentHarness.Agent.AgentHarness;

var baseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434";
var model = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "qwen3.5:9b";

var toolRegistry = new ToolRegistry([new GetCurrentTimeTool(), new HttpRequestTool()]);
var ollamaChatService = new OllamaChatService(baseUrl, model);
var agentLoop = new AgentLoop(ollamaChatService, toolRegistry);

var channel = Channel.CreateUnbounded<AgentRequest>();
var harness = new AgentRuntime(agentLoop, channel.Reader);
using var cts = new CancellationTokenSource();

var runTask = Task.Run(() => harness.RunAsync(cts.Token));

async Task<string> SendAsync(string userMessage)
{
    var request = new AgentRequest { UserMessage = userMessage };
    await channel.Writer.WriteAsync(request, cts.Token);
    return await request.Completion.Task;
}

try
{
    Console.WriteLine("AI Agent Harness is ready.");
    Console.WriteLine("Type your prompt and press Enter. Type 'quit' to exit.");

    while (true)
    {
        Console.Write("> ");
        var input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
        {
            continue;
        }

        if (string.Equals(input.Trim(), "quit", StringComparison.OrdinalIgnoreCase))
        {
            break;
        }

        var response = await SendAsync(input);
        Console.WriteLine($"assistant> {response}");
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Harness run failed: {exception.Message}");
    Environment.ExitCode = 1;
}
finally
{
    channel.Writer.Complete();
    cts.Cancel();
    await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(2)));
}
