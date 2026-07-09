using DCBot.Services.Misc;
using Discord;
using Discord.WebSocket;


namespace DCBot.Commands.ToggleGNMessage;

public sealed class ToggleGNMessageCommand : ICommand
{
	public string Name => "toggle_gn_message";
	public string Description => "Toggle the goodnight message on or off";
	public bool AllowAdmin => true;
	public bool AllowEveryone => false;
	public int Cooldown => 5;

	private readonly GoodnightService _goodnight;

	public ToggleGNMessageCommand(
		GoodnightService goodnight)
	{
		_goodnight = goodnight;
	}

	public async Task ExecuteAsync(SocketSlashCommand cmd)
	{
		await cmd.DeferAsync(ephemeral: true);

		var guildId = cmd.GuildId!.Value;
		var enabled = await _goodnight.ToggleAsync(guildId);

		var status = enabled ? "enabled" : "disabled";
		await cmd.FollowupAsync($"Goodnight message is now {status}.", ephemeral: true);
	}
}