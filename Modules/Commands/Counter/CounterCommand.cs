using DCBot.Core;
using DCBot.Services.Flavor;
using Discord;
using Discord.WebSocket;

namespace DCBot.Commands.Counter;

/// <summary>/counter — Member-Counter-Channel manuell aktualisieren (Admin).</summary>
public sealed class CounterCommand : ICommand
{
    public string Name => "counter";
    public string Description => "Update or show the member counter channel";
    public bool AllowAdmin => true;

    private readonly GuildConfigService _guildConfig;
    private readonly FlavorService _flavor;

    public CounterCommand(GuildConfigService guildConfig, FlavorService flavor)
    {
        _guildConfig = guildConfig;
        _flavor = flavor;
    }

    public async Task ExecuteAsync(SocketSlashCommand cmd)
    {
        if (cmd.GuildId is null || (cmd.User as SocketGuildUser)?.Guild is not { } guild)
        {
            await cmd.RespondAsync("Dieser Befehl funktioniert nur auf einem Server.", ephemeral: true);
            return;
        }

        await cmd.DeferAsync(ephemeral: true);

        var count = guild.MemberCount;
        var channelId = await _guildConfig.GetChannelAsync(guild.Id, "counterchannel");

        if (channelId is null || guild.GetChannel(channelId.Value) is not { } channel)
        {
            await cmd.FollowupAsync(embed: BuildEmbed(count, false, "counter channel not configured"),
                ephemeral: true);
            return;
        }

        try
        {
            var template = await _flavor.GetCounterTemplateAsync(guild.Id);
            var channelName = FlavorService.RenderCounterName(template, count);
            await channel.ModifyAsync(p => p.Name = channelName);
            await cmd.FollowupAsync(embed: BuildEmbed(count, true, $"updated <#{channel.Id}>"),
                ephemeral: true);
        }
        catch (Exception ex)
        {
            await cmd.FollowupAsync(embed: BuildEmbed(count, false, $"failed to update channel: {ex.Message}"),
                ephemeral: true);
        }
    }

    private static Embed BuildEmbed(int count, bool updated, string note) => new EmbedBuilder()
        .WithTitle("Server Counter")
        .WithDescription($"Members: {count}\n{note}")
        .WithColor(updated ? new Color(0x2ecc71) : new Color(0x992222))
        .Build();
}
