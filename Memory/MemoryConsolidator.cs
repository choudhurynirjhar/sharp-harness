using AgentHarness.Ollama;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentHarness.Memory;

public sealed class MemoryConsolidator
{
    private const string SystemPrompt = """
        You maintain long-term memory for an AI assistant. You receive the current memory document and a new conversation exchange.

        Extract only durable facts worth remembering across future sessions, such as:
        - User preferences, names, and roles
        - Ongoing projects, goals, and constraints
        - Important technical context the user shared

        Do NOT store greetings, small talk, transient tool output, or information that is only relevant to the current turn.

        Rules:
        - Merge new facts with the existing memory and deduplicate
        - Update contradictions using the newest information
        - Keep the result concise markdown with bullet points or short sections
        - If nothing new is worth saving, respond with exactly: NO_UPDATE
        - Output ONLY the updated markdown memory document, with no preamble or explanation
        """;

    private readonly OllamaChatService _ollamaChatService;
    private readonly LongTermMemoryStore _store;
    private readonly ILogger<MemoryConsolidator> _logger;

    public MemoryConsolidator(
        OllamaChatService ollamaChatService,
        LongTermMemoryStore store,
        ILogger<MemoryConsolidator>? logger = null)
    {
        _ollamaChatService = ollamaChatService;
        _store = store;
        _logger = logger ?? NullLogger<MemoryConsolidator>.Instance;
    }

    public async Task ConsolidateAsync(
        string userMessage,
        string assistantResponse,
        CancellationToken cancellationToken = default)
    {
        var existingMemory = await _store.ReadAsync(cancellationToken);

        var messages = new List<OllamaMessage>
        {
            new("system", SystemPrompt),
            new("user", BuildPrompt(existingMemory, userMessage, assistantResponse))
        };

        _logger.LogInformation("Consolidating long-term memory.");

        var result = await _ollamaChatService.ChatAsync(messages, [], cancellationToken);
        var updatedMemory = result.Text?.Trim();

        if (string.IsNullOrWhiteSpace(updatedMemory) ||
            string.Equals(updatedMemory, "NO_UPDATE", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Long-term memory consolidation produced no changes.");
            return;
        }

        await _store.WriteAsync(updatedMemory, cancellationToken);
        _logger.LogInformation("Long-term memory updated at {MemoryPath}.", _store.FilePath);
    }

    private static string BuildPrompt(string existingMemory, string userMessage, string assistantResponse)
    {
        var existingSection = string.IsNullOrWhiteSpace(existingMemory)
            ? "(empty)"
            : existingMemory.Trim();

        return $"""
            Existing long-term memory:
            {existingSection}

            New conversation exchange:
            User: {userMessage}
            Assistant: {assistantResponse}
            """;
    }
}
