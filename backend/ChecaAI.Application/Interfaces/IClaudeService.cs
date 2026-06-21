namespace ChecaAI.Application.Interfaces;

public interface IClaudeService
{
    /// <summary>
    /// Generates a complete (non-streaming) response from Claude.
    /// </summary>
    Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct = default);

    /// <summary>
    /// Streams a response from Claude using the Vercel AI SDK data stream protocol.
    /// Text chunks are written as: 0:"chunk"\n
    /// Finish signal: d:{"finishReason":"stop"}\n
    /// </summary>
    Task StreamAsync(string systemPrompt, string userMessage, Stream outputStream, CancellationToken ct = default);
}
