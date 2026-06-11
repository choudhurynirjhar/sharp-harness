using AgentHarness.Ollama;

namespace AgentHarness.Memory;

public sealed class ShortTermMemory
{
    private readonly List<OllamaMessage> _messages = [];

    public void AddTurn(string userMessage, string assistantResponse)
    {
        _messages.Add(new OllamaMessage("user", userMessage));
        _messages.Add(new OllamaMessage("assistant", assistantResponse));
    }

    public IReadOnlyList<OllamaMessage> GetMessages() => _messages.ToList();

    public void Clear() => _messages.Clear();
}
