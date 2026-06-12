using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Channels;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Services.Ai;

public enum AiBackend
{
    Disabled,
    OpenAi,
    Basti,
}

/// <summary>
/// AI service — port of the Go chatgpt module's handler package:
/// - backend switching (OpenAI / Basti API / disabled)
/// - per-channel conversation cache (5 min expiry, OpenAI only)
/// - serial processing queue (so requests never run in parallel)
/// - Basti API model load/unload + custom system prompt
/// </summary>
public sealed class AiService
{
    private const string BastiBaseUrl = "https://api.bastiwood.com";
    private const int MaxPromptLength = 100;

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly ILogger<AiService> _logger;

    // ---- backend state ----
    public AiBackend ActiveBackend { get; set; } = AiBackend.Disabled;
    public bool BastiLoaded { get; private set; }
    public string BastiPrompt { get; set; } = "";

    // ---- conversation cache (channelId -> messages) ----
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);
    private readonly Dictionary<ulong, List<(string Role, string Content, DateTimeOffset At)>> _cache = new();
    private readonly object _cacheLock = new();

    // ---- serial queue ----
    public sealed record QueueItem(SocketMessage Message, string Prompt);
    private readonly Channel<QueueItem> _queue =
        Channel.CreateBounded<QueueItem>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
        });

    public AiService(ILogger<AiService> logger)
    {
        _logger = logger;
        _ = Task.Run(WorkerLoopAsync); // StartWorker() equivalent
    }

    public static bool IsBastiAvailable()
        => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BASTIAPI"));

    /// <summary>Validate + clean the prompt (max 100 chars, not empty).</summary>
    public static string ValidateAndCleanPrompt(string prompt)
    {
        var cleaned = prompt.Trim();
        if (cleaned.Length == 0)
            throw new ArgumentException("Der Prompt darf nicht leer sein.");
        if (cleaned.Length > MaxPromptLength)
            throw new ArgumentException($"Der Prompt darf maximal {MaxPromptLength} Zeichen lang sein.");
        return cleaned;
    }

    /// <summary>Try to enqueue a message for processing. False = queue full.</summary>
    public bool TryEnqueue(SocketMessage message, string prompt)
        => _queue.Writer.TryWrite(new QueueItem(message, prompt));

    // ================= queue worker =================

    private async Task WorkerLoopAsync()
    {
        await foreach (var item in _queue.Reader.ReadAllAsync())
        {
            try
            {
                await ProcessItemAsync(item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AI] Worker failed for message {Id}", item.Message.Id);
            }
        }
    }

    private async Task ProcessItemAsync(QueueItem item)
    {
        string reply;
        try
        {
            reply = ActiveBackend == AiBackend.OpenAi
                ? await GetOpenAiResponseAsync(item.Prompt, item.Message.Channel.Id)
                : await GetBastiResponseAsync(item.Prompt);

            if (ActiveBackend == AiBackend.OpenAi)
                AddMessageToCache(item.Message.Channel.Id, "assistant", reply);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AI] Backend error");
            await ReplyAsync(item.Message, "❌ Fehler bei der Verarbeitung: " + ex.Message);
            return;
        }

        if (reply.Length > 2000)
            reply = reply[..1997] + "...";

        await ReplyAsync(item.Message, reply);
    }

    private static async Task ReplyAsync(SocketMessage message, string content)
    {
        await message.Channel.SendMessageAsync(
            content,
            messageReference: new MessageReference(message.Id, message.Channel.Id),
            allowedMentions: new AllowedMentions { MentionRepliedUser = true });
    }

    // ================= conversation cache =================

    public void AddMessageToCache(ulong channelId, string role, string content)
    {
        lock (_cacheLock)
        {
            if (!_cache.TryGetValue(channelId, out var list))
            {
                list = new List<(string, string, DateTimeOffset)>();
                _cache[channelId] = list;
            }

            list.Add((role, content, DateTimeOffset.UtcNow));

            var cutoff = DateTimeOffset.UtcNow - CacheExpiry;
            list.RemoveAll(m => m.At < cutoff);
        }
    }

    private List<(string Role, string Content)> GetConversationHistory(ulong channelId)
    {
        lock (_cacheLock)
        {
            if (!_cache.TryGetValue(channelId, out var list))
                return new List<(string, string)>();

            var cutoff = DateTimeOffset.UtcNow - CacheExpiry;
            return list.Where(m => m.At >= cutoff)
                       .Select(m => (m.Role, m.Content))
                       .ToList();
        }
    }

    // ================= OpenAI backend =================

    private sealed record ChatMessage(string role, string content);
    private sealed record ChatRequest(string model, List<ChatMessage> messages, double temperature, int max_tokens);
    private sealed record ChatChoice(ChatMessage? message);
    private sealed record ChatResponse(List<ChatChoice>? choices);

    private async Task<string> GetOpenAiResponseAsync(string userPrompt, ulong channelId)
    {
        var token = Environment.GetEnvironmentVariable("GPT_TOKEN")
            ?? throw new InvalidOperationException("GPT_TOKEN environment variable not set");

        var messages = new List<ChatMessage> { new("system", Personality.SystemPrompt) };
        foreach (var (role, histContent) in GetConversationHistory(channelId))
            messages.Add(new ChatMessage(role, histContent));
        messages.Add(new ChatMessage("user", userPrompt));

        using var request = new HttpRequestMessage(HttpMethod.Post,
            "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(
            new ChatRequest("gpt-4o-mini", messages, 0.7, 150));

        using var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"OpenAI request failed: {(int)response.StatusCode} {body[..Math.Min(body.Length, 300)]}");
        }

        var result = await response.Content.ReadFromJsonAsync<ChatResponse>();
        var content = result?.choices?.FirstOrDefault()?.message?.content?.Trim();
        if (string.IsNullOrEmpty(content))
            throw new InvalidOperationException("No response from OpenAI");
        return content;
    }

    // ================= Basti backend =================

    private static string GetBastiKey()
        => Environment.GetEnvironmentVariable("BASTIAPI")
           ?? throw new InvalidOperationException("BASTIAPI env var is not set");

    private HttpRequestMessage CreateBastiRequest(HttpMethod method, string pathAndQuery)
    {
        var key = GetBastiKey();
        var request = new HttpRequestMessage(method, BastiBaseUrl + pathAndQuery);
        request.Headers.Add("X-API-Key", key);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return request;
    }

    public async Task LoadBastiAsync()
    {
        using var request = CreateBastiRequest(HttpMethod.Post, "/loadmodel");
        using var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        BastiLoaded = true;
    }

    public async Task UnloadBastiAsync()
    {
        BastiPrompt = "";
        BastiLoaded = false;
        using var request = CreateBastiRequest(HttpMethod.Post, "/unloadmodel");
        using var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private sealed record BastiResponse(string? generated_text, string? text);

    private async Task<string> GetBastiResponseAsync(string message)
    {
        var query = $"/generatetext?input_text={Uri.EscapeDataString(message)}";
        if (!string.IsNullOrEmpty(BastiPrompt))
            query += $"&system_prompt={Uri.EscapeDataString(BastiPrompt)}";

        using var request = CreateBastiRequest(HttpMethod.Get, query);
        using var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Basti request failed: {(int)response.StatusCode} {body[..Math.Min(body.Length, 300)]}");
        }

        var result = await response.Content.ReadFromJsonAsync<BastiResponse>();
        return result?.generated_text ?? result?.text ?? "";
    }
}
