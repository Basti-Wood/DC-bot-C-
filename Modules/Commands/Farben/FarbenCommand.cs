using DCBot.Services.Booster;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DCBot.Commands.Farben;

/// <summary>
/// /farben — Farb-Rolle für Booster (Port von farben_command.go).
/// Braucht die "Booster_role"-Rolle. "Rainbow" aktiviert die Rotation.
/// </summary>
public sealed class FarbenCommand : ICommand
{
    public string Name => "farben";
    public string Description => "Wähle eine Rolle/Farbe (nur mit Booster)";
    public bool AllowEveryone => true;

    private readonly BoosterService _booster;
    private readonly ILogger<FarbenCommand> _logger;

    public FarbenCommand(BoosterService booster, ILogger<FarbenCommand> logger)
    {
        _booster = booster;
        _logger = logger;
    }

    public List<SlashCommandOptionBuilder> BuildOptions()
    {
        var option = new SlashCommandOptionBuilder()
            .WithName("wahl")
            .WithDescription("Rolle/Farbe oder 'Rainbow' (Name aus /flavor booster möglich)")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true);
        return new List<SlashCommandOptionBuilder> { option };
    }

    public async Task ExecuteAsync(SocketSlashCommand cmd)
    {
        if (cmd.User is not SocketGuildUser member)
        {
            await cmd.RespondAsync("Dieser Befehl funktioniert nur auf einem Server.", ephemeral: true);
            return;
        }

        await cmd.DeferAsync(ephemeral: true);

        var choice = ((string)cmd.Data.Options.First(o => o.Name == "wahl").Value).Trim();
        var guild = member.Guild;
        var roleMap = await _booster.GetRoleMapAsync(guild.Id);
        var labelMap = await _booster.GetRoleLabelsAsync(guild.Id);

        var boosterRoleId = roleMap.GetValueOrDefault(BoosterService.BoosterGateRoleKey);
        var boosterRoleLabel = labelMap.GetValueOrDefault(
            BoosterService.BoosterGateRoleKey,
            BoosterService.GetDefaultRoleLabel(BoosterService.BoosterGateRoleKey));
        if (boosterRoleId is null)
        {
            await RespondEmbedAsync(cmd, $"{boosterRoleLabel} fehlt",
                $"Die Rolle '{boosterRoleLabel}' ist auf diesem Server nicht konfiguriert. lol", 0xe74c3c, member);
            return;
        }

        if (member.Roles.All(r => r.Id != boosterRoleId))
        {
            await RespondEmbedAsync(cmd, "Nicht special :((((",
                $"Du benötigst die Rolle '{boosterRoleLabel}', um diesen Befehl zu verwenden :(", 0xe74c3c, member);
            return;
        }

        if (string.Equals(choice, "Rainbow", StringComparison.OrdinalIgnoreCase))
        {
            await _booster.SetRainbowActiveAsync(guild.Id, member.Id, true);
            await RespondEmbedAsync(cmd, "Rainbow Modus",
                "Rainbow Modus aktiviert! Deine Farbe ändert sich nun alle 30 Minuten. YAY", 0x2ecc71, member);
            return;
        }

        await _booster.SetRainbowActiveAsync(guild.Id, member.Id, false);

        var selectedRoleKey = await _booster.ResolveRoleKeyByLabelAsync(guild.Id, choice);
        if (selectedRoleKey is null)
        {
            var allowed = string.Join(", ", labelMap.Values.Select(v => $"'{v}'"));
            await RespondEmbedAsync(cmd, "Ungültige Auswahl",
                $"Die gewählte Option ist ungültig. Verfügbar: {allowed}", 0xe74c3c, member);
            return;
        }

        var selectedRoleId = roleMap.GetValueOrDefault(selectedRoleKey);
        var selectedRoleLabel = labelMap.GetValueOrDefault(selectedRoleKey, selectedRoleKey);
        if (selectedRoleId is null)
        {
            await RespondEmbedAsync(cmd, "Rolle nicht konfiguriert",
                $"Die Rolle '{selectedRoleLabel}' ist nicht konfiguriert LOL meld dich an musa", 0xe74c3c, member);
            return;
        }

        // Alle anderen wählbaren Rollen entfernen, gewünschte hinzufügen
        foreach (var roleKey in BoosterService.SelectableRoleKeys)
        {
            if (string.Equals(roleKey, selectedRoleKey, StringComparison.OrdinalIgnoreCase))
                continue;

            var roleId = roleMap.GetValueOrDefault(roleKey);
            if (roleId is null || roleId == selectedRoleId) continue;
            if (member.Roles.Any(r => r.Id == roleId))
            {
                try { await member.RemoveRoleAsync(roleId.Value); }
                catch (Exception ex) { _logger.LogWarning(ex, "farben: remove role {Role}", roleId); }
            }
        }

        try
        {
            await member.AddRoleAsync(selectedRoleId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "farben: failed to add role {Role} to {User}", selectedRoleId, member.Id);
            await RespondEmbedAsync(cmd, "Fehler", "Fehler beim Hinzufügen der Rolle.", 0xe74c3c, member);
            return;
        }

        await RespondEmbedAsync(cmd, $"Farbe bekommen: {selectedRoleLabel}",
            $"Du hast die Farbe '{selectedRoleLabel}' erhalten. YAY", 0x2ecc71, member);
    }

    private static async Task RespondEmbedAsync(
        SocketSlashCommand cmd, string title, string description, uint color, SocketGuildUser member)
    {
        var builder = new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(new Color(color));

        var avatar = member.GetDisplayAvatarUrl(size: 1024);
        if (!string.IsNullOrEmpty(avatar))
            builder.WithThumbnailUrl(avatar);

        await cmd.FollowupAsync(embed: builder.Build(), ephemeral: true);
    }
}
