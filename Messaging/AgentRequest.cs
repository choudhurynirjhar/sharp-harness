namespace AgentHarness.Messaging;

public sealed class AgentRequest
{
    public required string UserMessage { get; init; }

    public TaskCompletionSource<string> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
