using AgentHarness.Ollama;

namespace AgentHarness.Memory;

public sealed class AgentMemory
{
    private readonly ShortTermMemory _shortTermMemory;
    private readonly LongTermMemoryStore _longTermMemoryStore;
    private readonly MemoryConsolidator _memoryConsolidator;

    public AgentMemory(
        ShortTermMemory shortTermMemory,
        LongTermMemoryStore longTermMemoryStore,
        MemoryConsolidator memoryConsolidator)
    {
        _shortTermMemory = shortTermMemory;
        _longTermMemoryStore = longTermMemoryStore;
        _memoryConsolidator = memoryConsolidator;
    }

    public async Task<IReadOnlyList<OllamaMessage>> BuildContextMessagesAsync(
        string currentUserMessage,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<OllamaMessage>();

        var longTermMemory = await _longTermMemoryStore.ReadAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(longTermMemory))
        {
            messages.Add(new OllamaMessage(
                "system",
                $"Long-term memory from previous sessions:\n\n{longTermMemory.Trim()}"));
        }

        messages.AddRange(_shortTermMemory.GetMessages());
        messages.Add(new OllamaMessage("user", currentUserMessage));

        return messages;
    }

    public async Task RecordTurnAsync(
        string userMessage,
        string assistantResponse,
        CancellationToken cancellationToken = default)
    {
        _shortTermMemory.AddTurn(userMessage, assistantResponse);
        await _memoryConsolidator.ConsolidateAsync(userMessage, assistantResponse, cancellationToken);
    }
}
