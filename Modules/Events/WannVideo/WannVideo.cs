using System.Collections.Concurrent;
using DCBot.Core;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Events.WannVideo;

public sealed partial class WannVideo : IEventListener
{
	public string Name => "WannVideo";
	public static readonly string[] TriggerWords = new[] { "wann", "video"};

	private readonly GuildConfigService _guildConfig;
	private readonly ILogger<WannVideo> _logger;

	public WannVideo(
		GuildConfigService guildConfig,
		ILogger<WannVideo> logger)
	{
		_guildConfig = guildConfig;
		_logger = logger;
	}

	public void Register(EventManager events)
	{
		events.OnMessageReceived(HandleAsync);
	}

	private async Task<bool> IsValidMessage(SocketMessage message)
	{
		if (message.Author.IsBot) return false;
		if (message.Channel is not SocketGuildChannel guildChannel) return false;

		var content = message.Content.ToLowerInvariant();
		return TriggerWords.All(word => content.Contains(word));
	}


	private async Task HandleAsync(SocketMessage message)
	{
		if (message.Author.IsBot) return;
		if (message.Channel is not SocketGuildChannel guildChannel) return;

		var content = message.Content.ToLowerInvariant();
		if (await IsValidMessage(message))
		{
			var replyTo = new MessageReference(message.Id);
			await message.Channel.SendMessageAsync("Morgen. wenn nicht, dann lies diese nachicht nocheinmal :3", messageReference: replyTo);
			_logger.LogInformation("Responded to 'wann video' in channel {ChannelId}", message.Channel.Id);
		}
	}
}