using DCBot.Services.Ai;
using Discord;
using Discord.WebSocket;

namespace DCBot.Commands.SetPrompt;

/// <summary>/setprompt — setzt den System-Prompt für das Basti-Backend (Admin).</summary>
public sealed class SetPromptCommand : ICommand
{
    public string Name => "setprompt";
    public string Description => "Setzt den System-Prompt für das Basti-AI-Backend";
    public bool AllowAdmin => true;

    private readonly AiService _ai;

    public SetPromptCommand(AiService ai)
    {
        _ai = ai;
    }

    public List<SlashCommandOptionBuilder> BuildOptions() => new()
    {
        new SlashCommandOptionBuilder()
            .WithName("prompt")
            .WithDescription("Der neue System-Prompt (leer lassen zum Zurücksetzen)")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(false),
    };

    public async Task ExecuteAsync(SocketSlashCommand cmd)
    {
        var prompt = cmd.Data.Options.FirstOrDefault(o => o.Name == "prompt")?.Value as string ?? "";
        _ai.BastiPrompt = prompt;

        await cmd.RespondAsync(
            string.IsNullOrEmpty(prompt)
                ? "System-Prompt zurückgesetzt."
                : $"System-Prompt gesetzt ({prompt.Length} Zeichen).",
            ephemeral: true);
    }
}
