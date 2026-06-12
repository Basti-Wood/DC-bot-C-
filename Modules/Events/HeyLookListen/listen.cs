using System.Collections.Concurrent;
using DCBot.Core;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

/// <summary>
/// Navis HeyLookListen-Event
/// - wenn viele nachichten in kurzer Zeit in einem Kanal gepostet werden,
/// reagiert Navi auf 3 nachichten mit jeweils Hey, Look und Listen
/// </summary>

namespace DCBot.Events.HeyLookListen;

public sealed partial class HeyLookListen : IEventListener
{
	public string Name => "HeyLookListen";
	private const string WindowConfigKey = "heylooklisten:window_seconds";
	private const int DefaultWindowSeconds = 10;
	private const int MinWindowSeconds = 0;
	private const int MaxWindowSeconds = 120;
	private static readonly TimeSpan TriggerCooldown = TimeSpan.FromMinutes(1);

	private readonly GuildConfigService _guildConfig;
	private readonly ILogger<HeyLookListen> _logger;

	// channelId -> timestamps of recent messages
	private readonly ConcurrentDictionary<ulong, Queue<DateTimeOffset>> _messageTimes = new();
	private readonly ConcurrentDictionary<ulong, DateTimeOffset> _sleepUntilByChannel = new();

	public HeyLookListen(
		GuildConfigService guildConfig,
		ILogger<HeyLookListen> logger)
	{
		_guildConfig = guildConfig;
		_logger = logger;
	}

	public void Register(EventManager events)
	{
		events.OnMessageReceived(HandleAsync);
	}

	private async Task HandleAsync(SocketMessage message)
	{
		if (message.Author.IsBot) return;
		if (message.Channel is not SocketGuildChannel guildChannel) return;

		var channelId = message.Channel.Id;
		var now = DateTimeOffset.UtcNow;
		var window = await GetWindowAsync(guildChannel.Guild.Id);

		if (window == TimeSpan.Zero)
		{
			// Fully disable behavior and clear any pending channel state.
			_messageTimes.TryRemove(channelId, out _);
			_sleepUntilByChannel.TryRemove(channelId, out _);
			return;
		}

		if (_sleepUntilByChannel.TryGetValue(channelId, out var sleepUntil) && now < sleepUntil)
			return;
		if (sleepUntil <= now)
			_sleepUntilByChannel.TryRemove(channelId, out _);

		var timesQueue = _messageTimes.GetOrAdd(channelId, _ => new Queue<DateTimeOffset>());

		lock (timesQueue)
		{
			if (_sleepUntilByChannel.TryGetValue(channelId, out var lockSleepUntil) && now < lockSleepUntil)
				return;

			// Remove timestamps outside configured window
			while (timesQueue.Count > 0 && (now - timesQueue.Peek()) > window)
				timesQueue.Dequeue();

			timesQueue.Enqueue(now);

			if (timesQueue.Count >= 3)
			{
				// Reset after triggering so future bursts can trigger again.
				timesQueue.Clear();
				_sleepUntilByChannel[channelId] = now.Add(TriggerCooldown);

				// Post "Hey", "Look", "Listen" in sequence with 1s delay
				_ = Task.Run(async () =>
				{
					try
					{
						var replyTo = await GetLatestMessageReferenceAsync(message.Channel, message.Id);
						await message.Channel.SendMessageAsync("Hey", messageReference: replyTo);
						await Task.Delay(500);
						replyTo = await GetLatestMessageReferenceAsync(message.Channel, message.Id);
						await message.Channel.SendMessageAsync("Look", messageReference: replyTo);
						await Task.Delay(500);
						replyTo = await GetLatestMessageReferenceAsync(message.Channel, message.Id);
						await message.Channel.SendMessageAsync("Listen", messageReference: replyTo);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error sending HeyLookListen messages");
					}
				});
			}
		}
	}

	private static async Task<MessageReference> GetLatestMessageReferenceAsync(
		ISocketMessageChannel channel,
		ulong fallbackMessageId)
	{
		try
		{
			// Fetch a small batch so we can skip bot-authored messages.
			var latestBatch = await channel.GetMessagesAsync(limit: 10).FlattenAsync();
			var latestUserMessage = latestBatch.FirstOrDefault(m => !m.Author.IsBot);
			return new MessageReference(latestUserMessage?.Id ?? fallbackMessageId);
		}
		catch
		{
			return new MessageReference(fallbackMessageId);
		}
	}

	private async Task<TimeSpan> GetWindowAsync(ulong guildId)
	{
		var seconds = DefaultWindowSeconds;
		try
		{
			var configured = await _guildConfig.GetAsync(guildId, WindowConfigKey);
			if (int.TryParse(configured, out var parsed))
				seconds = Math.Clamp(parsed, MinWindowSeconds, MaxWindowSeconds);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to read HeyLookListen window for guild {Guild}", guildId);
		}

		return TimeSpan.FromSeconds(seconds);
	}
}
