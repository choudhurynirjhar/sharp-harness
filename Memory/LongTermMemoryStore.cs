namespace AgentHarness.Memory;

public sealed class LongTermMemoryStore
{
    public const string HarnessDirectoryName = ".harness";
    public const string MemoryFileName = "memory.md";

    private readonly string _memoryFilePath;

    public LongTermMemoryStore(string? workingDirectory = null)
    {
        var root = workingDirectory ?? Directory.GetCurrentDirectory();
        _memoryFilePath = Path.Combine(root, HarnessDirectoryName, MemoryFileName);
    }

    public string FilePath => _memoryFilePath;

    public async Task<string> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_memoryFilePath))
        {
            return string.Empty;
        }

        return await File.ReadAllTextAsync(_memoryFilePath, cancellationToken);
    }

    public async Task WriteAsync(string content, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_memoryFilePath)
            ?? throw new InvalidOperationException($"Invalid memory file path: {_memoryFilePath}");

        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(_memoryFilePath, content.TrimEnd() + Environment.NewLine, cancellationToken);
    }
}
