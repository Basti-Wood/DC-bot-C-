using System.Text.RegularExpressions;
using DCBot.Services.Ai;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Events.AiChat;

/// <summary>
/// AI-Antworten bei Mention oder Reply (Port von ai_events.go):
/// - reagiert, wenn der Bot erwähnt wird ODER auf eine Bot-Nachricht
///   geantwortet wird
/// - Prompt wird bereinigt + validiert (max. 100 Zeichen)
/// - Nachricht landet in der seriellen AI-Queue (Antwort als Reply)
/// - Fehlermeldungen löschen sich nach 2 Sekunden selbst
/// </summary>
public sealed partial class AiMentionEvent : IEventListener
{
    public string Name => "AiChat";

    private readonly DiscordSocketClient _client;
    private readonly AiService _ai;
    private readonly ILogger<AiMentionEvent> _logger;

    public AiMentionEvent(
        DiscordSocketClient client,
        AiService ai,
        ILogger<AiMentionEvent> logger)
    {
        _client = client;
        _ai = ai;
        _logger = logger;
    }

    public void Register(EventManager events)
    {
        events.OnMessageReceived(HandleAsync);
    }

    [GeneratedRegex(@"<@!?\d+>")]
    private static partial Regex MentionRegex();

    private async Task HandleAsync(SocketMessage message)
    {
        if (message.Author.IsBot) return;
        if (message.Channel is not SocketGuildChannel) return;
        if (message is not SocketUserMessage userMessage) return;

        // AI global deaktiviert
        if (_ai.ActiveBackend == AiBackend.Disabled) return;

        // Basti-Backend braucht ein geladenes Modell
        if (_ai.ActiveBackend == AiBackend.Basti && !_ai.BastiLoaded) return;

        var botId = _client.CurrentUser?.Id;
        if (botId is null) return;

        var mentioned = userMessage.MentionedUsers.Any(u => u.Id == botId);
        var isReply = userMessage.ReferencedMessage?.Author?.Id == botId;

        if (!mentioned && !isReply) return;

        _logger.LogDebug("[AI] Bot mentioned by {User}: {Content}",
            message.Author.Username, message.Content);

        var prompt = MentionRegex().Replace(message.Content, "").Trim();

        string cleanPrompt;
        try
        {
            cleanPrompt = AiService.ValidateAndCleanPrompt(prompt);
        }
        catch (ArgumentException ex)
        {
            await SendSelfDeletingMessageAsync(message.Channel, $"❌ {ex.Message}");
            return;
        }

        // Verlauf nur für OpenAI (Basti hat keinen Conversation-Verlauf)
        if (_ai.ActiveBackend == AiBackend.OpenAi)
            _ai.AddMessageToCache(message.Channel.Id, "user", cleanPrompt);

        try { await message.Channel.TriggerTypingAsync(); } catch { /* not critical */ }

        if (!_ai.TryEnqueue(message, cleanPrompt))
        {
            await SendSelfDeletingMessageAsync(message.Channel,
                "Ich bin gerade beschäftigt, bitte versuch es gleich nochmal.");
        }
    }

    private async Task SendSelfDeletingMessageAsync(ISocketMessageChannel channel, string content)
    {
        try
        {
            if (content.Length > 2000)
                content = content[..1997] + "...";

            var msg = await channel.SendMessageAsync(content);
            await Task.Delay(TimeSpan.FromSeconds(2));
            await msg.DeleteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AI] Failed to send/delete self-deleting message");
        }
    }
}
