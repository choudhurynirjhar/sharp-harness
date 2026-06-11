using System.Threading.Channels;
using AgentHarness.Agent;
using AgentHarness.Evaluation;
using AgentHarness.Memory;
using AgentHarness.Messaging;
using AgentHarness.Ollama;
using AgentHarness.Tools;
using AgentRuntime = AgentHarness.Agent.AgentHarness;
using Microsoft.Extensions.Logging;

var baseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434";
var model = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "qwen3.5:9b";

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

var evaluationLogger = new AgentEvaluationLogger(loggerFactory.CreateLogger<AgentEvaluationLogger>());

var toolRegistry = new ToolRegistry([new GetCurrentTimeTool(), new HttpRequestTool(), new CommandPromptTool()]);
var ollamaChatService = new OllamaChatService(
    baseUrl,
    model,
    loggerFactory.CreateLogger<OllamaChatService>());

var longTermMemoryStore = new LongTermMemoryStore();
var shortTermMemory = new ShortTermMemory();
var memoryConsolidator = new MemoryConsolidator(
    ollamaChatService,
    longTermMemoryStore,
    loggerFactory.CreateLogger<MemoryConsolidator>());
var agentMemory = new AgentMemory(shortTermMemory, longTermMemoryStore, memoryConsolidator);

var agentLoop = new AgentLoop(
    ollamaChatService,
    toolRegistry,
    agentMemory,
    loggerFactory.CreateLogger<AgentLoop>());

var channel = Channel.CreateUnbounded<AgentRequest>();
var harness = new AgentRuntime(agentLoop, channel.Reader, evaluationLogger);
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
    Console.WriteLine($"Long-term memory file: {longTermMemoryStore.FilePath}");
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
