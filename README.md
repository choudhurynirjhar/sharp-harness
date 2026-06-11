# Agent Harness

A minimal C# harness for running a local AI agent. The goal is to provide a small, readable foundation for experimenting with agent loops—not a full framework.

It connects to [Ollama](https://ollama.com), runs a tool-calling agent loop, and keeps conversation context across messages with short-term (in-memory) and long-term (`.harness/memory.md`) memory.

## Run

```bash
dotnet run
```

Optional environment variables:

- `OLLAMA_BASE_URL` (default: `http://localhost:11434`)
- `OLLAMA_MODEL` (default: `qwen3.5:9b`)
