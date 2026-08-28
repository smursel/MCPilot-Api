namespace MCPilot.Application.Abstractions;

public sealed class LlmProviderException(string message, Exception? innerException = null)
    : Exception(message, innerException);
