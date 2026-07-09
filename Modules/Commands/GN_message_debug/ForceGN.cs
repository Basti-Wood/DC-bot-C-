using DCBot.Services.Misc;
using Discord;
using Discord.WebSocket;


namespace DCBot.Commands.ForceGNMessage;

public sealed class ForceGNMessageCommand : ICommand
{
	public string Name => "force_gn_message";
	public string Description => "Force the goodnight message to be sent immediately";
	public bool AllowAdmin => true;
	public bool AllowEveryone => false;
	public int Cooldown => 5;

	private readonly GoodnightService _goodnight;

	public ForceGNMessageCommand(
		GoodnightService goodnight)
	{
		_goodnight = goodnight;
	}

	public async Task ExecuteAsync(SocketSlashCommand cmd)
	{
		await cmd.DeferAsync(ephemeral: true);

		var guildId = cmd.GuildId!.Value;

		if (!await _goodnight.IsEnabledAsync(guildId))
		{
			await cmd.FollowupAsync("Goodnight message is currently disabled. Please enable it first.", ephemeral: true);
			return;
		}

		if (await _goodnight.GetTargetChannelIdAsync(guildId) is null)
		{
			await cmd.FollowupAsync("Goodnight channel is not configured. Please set the main channel first.", ephemeral: true);
			return;
		}

		var sent = await _goodnight.SendNowAsync(guildId);
		if (!sent)
		{
			await cmd.FollowupAsync("Goodnight channel not found. Please check the configuration.", ephemeral: true);
			return;
		}

		await cmd.FollowupAsync("Goodnight message sent successfully.", ephemeral: true);
	}
}