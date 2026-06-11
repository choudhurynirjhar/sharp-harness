namespace AgentHarness.Ollama;

public sealed record OllamaUsage(
    string? Model,
    bool Done,
    string? DoneReason,
    long? TotalDurationNs,
    long? LoadDurationNs,
    int? PromptEvalCount,
    long? PromptEvalDurationNs,
    int? EvalCount,
    long? EvalDurationNs)
{
    public int? TotalTokens =>
        PromptEvalCount is null && EvalCount is null
            ? null
            : (PromptEvalCount ?? 0) + (EvalCount ?? 0);

    public double? TokensPerSecond =>
        EvalCount is > 0 and var count && EvalDurationNs is > 0
            ? count / (EvalDurationNs.Value / 1_000_000_000.0)
            : null;
}
