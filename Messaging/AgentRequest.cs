namespace AgentHarness.Messaging;

public sealed class AgentRequest
{
    public Guid RequestId { get; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;

    public required string UserMessage { get; init; }

    public TaskCompletionSource<string> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
