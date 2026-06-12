using DCBot.Services.Ai;
using Discord;
using Discord.WebSocket;

namespace DCBot.Commands.ChangeAi;

/// <summary>/changeai — AI-Backend wechseln: OpenAI / Basti / Disabled (Admin).</summary>
public sealed class ChangeAiCommand : ICommand
{
    public string Name => "changeai";
    public string Description => "AI-Backend wechseln (OpenAI / Basti / Disabled)";
    public bool AllowAdmin => true;

    private readonly AiService _ai;

    public ChangeAiCommand(AiService ai)
    {
        _ai = ai;
    }

    public List<SlashCommandOptionBuilder> BuildOptions() => new()
    {
        new SlashCommandOptionBuilder()
            .WithName("backend")
            .WithDescription("Welches Backend genutzt werden soll")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true)
            .AddChoice("OpenAI", "openai")
            .AddChoice("Basti", "basti")
            .AddChoice("Disabled", "disabled"),
    };

    public async Task ExecuteAsync(SocketSlashCommand cmd)
    {
        var choice = (string)cmd.Data.Options.First(o => o.Name == "backend").Value;

        switch (choice)
        {
            case "openai":
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GPT_TOKEN")))
                {
                    await cmd.RespondAsync("GPT_TOKEN ist nicht gesetzt — OpenAI nicht verfügbar.", ephemeral: true);
                    return;
                }
                _ai.ActiveBackend = AiBackend.OpenAi;
                await cmd.RespondAsync("AI-Backend: **OpenAI** ✅", ephemeral: true);
                break;

            case "basti":
                if (!AiService.IsBastiAvailable())
                {
                    await cmd.RespondAsync("BASTIAPI ist nicht gesetzt — Basti nicht verfügbar.", ephemeral: true);
                    return;
                }
                _ai.ActiveBackend = AiBackend.Basti;
                var hint = _ai.BastiLoaded ? "" : " (Modell noch nicht geladen — nutze `/loadai`)";
                await cmd.RespondAsync($"AI-Backend: **Basti** ✅{hint}", ephemeral: true);
                break;

            case "disabled":
                _ai.ActiveBackend = AiBackend.Disabled;
                await cmd.RespondAsync("AI ist jetzt **deaktiviert**.", ephemeral: true);
                break;

            default:
                await cmd.RespondAsync("Unbekanntes Backend.", ephemeral: true);
                break;
        }
    }
}
