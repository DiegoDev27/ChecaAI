using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ChecaAI.Application.Interfaces;

namespace ChecaAI.Infrastructure.Services;

/// <summary>
/// Calls the Anthropic Messages API (claude-opus-4-5 or claude-sonnet-4-5) to generate AI summaries
/// and analysis. Supports both full-response and streaming (Vercel AI SDK data stream protocol).
///
/// Config key: AnthropicApiKey
/// Get yours at: console.anthropic.com
/// </summary>
public class ClaudeService : IClaudeService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClaudeService> _logger;

    private const string AnthropicMessagesUrl = "https://api.anthropic.com/v1/messages";
    private const string ModelId = "claude-opus-4-5";
    private const int MaxTokens = 1024;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ClaudeService(HttpClient httpClient, IConfiguration configuration, ILogger<ClaudeService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var apiKey = configuration["AnthropicApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            _logger.LogWarning("[ClaudeService] AnthropicApiKey not configured — AI features will return errors");
        else
        {
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", apiKey);
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        }

        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <inheritdoc/>
    public async Task<string> GenerateAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        var requestBody = new AnthropicRequest
        {
            Model = ModelId,
            MaxTokens = MaxTokens,
            System = systemPrompt,
            Messages = [new AnthropicMessage { Role = "user", Content = userMessage }]
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOpts);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(AnthropicMessagesUrl, content, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ClaudeService] HTTP error calling Anthropic API");
            return "Erro ao conectar ao serviço de IA. Tente novamente mais tarde.";
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("[ClaudeService] Anthropic API error {Status}: {Body}", response.StatusCode, errorBody);
            return $"Erro ao gerar resposta: {response.StatusCode}";
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var parsed = JsonSerializer.Deserialize<AnthropicResponse>(responseJson, JsonOpts);

        return parsed?.Content?.FirstOrDefault()?.Text ?? string.Empty;
    }

    /// <inheritdoc/>
    public async Task StreamAsync(
        string systemPrompt, string userMessage, Stream outputStream, CancellationToken ct = default)
    {
        var requestBody = new AnthropicRequest
        {
            Model = ModelId,
            MaxTokens = MaxTokens,
            System = systemPrompt,
            Stream = true,
            Messages = [new AnthropicMessage { Role = "user", Content = userMessage }]
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOpts);
        using var requestContent = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(AnthropicMessagesUrl, requestContent, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ClaudeService] HTTP error calling Anthropic streaming API");
            await WriteVercelErrorAsync(outputStream, "Erro ao conectar ao serviço de IA.", ct);
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("[ClaudeService] Anthropic streaming error {Status}: {Body}", response.StatusCode, errorBody);
            await WriteVercelErrorAsync(outputStream, $"Erro {response.StatusCode} ao gerar resposta.", ct);
            return;
        }

        // Parse Anthropic SSE stream and emit Vercel AI SDK data stream protocol
        await using var responseStream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(responseStream, Encoding.UTF8);
        var writer = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

        string? currentEvent = null;
        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) break;

            if (line.StartsWith("event: "))
            {
                currentEvent = line["event: ".Length..].Trim();
                continue;
            }

            if (!line.StartsWith("data: ")) continue;
            var data = line["data: ".Length..].Trim();
            if (data == "[DONE]") break;

            // We only care about content_block_delta events
            if (currentEvent != "content_block_delta") continue;

            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;

                if (!root.TryGetProperty("delta", out var delta)) continue;
                if (!delta.TryGetProperty("type", out var typeEl)) continue;
                if (typeEl.GetString() != "text_delta") continue;
                if (!delta.TryGetProperty("text", out var textEl)) continue;

                var text = textEl.GetString() ?? "";
                if (string.IsNullOrEmpty(text)) continue;

                // Vercel AI SDK data stream protocol — text chunk
                // Format: 0:"<json-escaped-text>"\n
                var escaped = JsonSerializer.Serialize(text);
                await writer.WriteAsync($"0:{escaped}\n");
                await outputStream.FlushAsync(ct);
            }
            catch (JsonException)
            {
                // Ignore malformed chunks
            }
        }

        // Signal completion in Vercel AI SDK format
        await writer.WriteAsync("d:{\"finishReason\":\"stop\"}\n");
        await outputStream.FlushAsync(ct);
    }

    private static async Task WriteVercelErrorAsync(Stream stream, string message, CancellationToken ct)
    {
        var escaped = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes($"0:{escaped}\nd:{{\"finishReason\":\"error\"}}\n");
        await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private sealed class AnthropicRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = ModelId;

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; } = 1024;

        [JsonPropertyName("system")]
        public string? System { get; set; }

        [JsonPropertyName("messages")]
        public List<AnthropicMessage> Messages { get; set; } = new();

        [JsonPropertyName("stream")]
        public bool? Stream { get; set; }
    }

    private sealed class AnthropicMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "user";

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class AnthropicResponse
    {
        [JsonPropertyName("content")]
        public List<AnthropicContentBlock>? Content { get; set; }
    }

    private sealed class AnthropicContentBlock
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
