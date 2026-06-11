using DCBot.Services.Roleplay;
using Discord;
using Discord.WebSocket;

namespace DCBot.Commands.SwitchApi;

/// <summary>/switchapi — GIF-Quelle wechseln: Otaku / Basti / Both (Admin).</summary>
public sealed class SwitchApiCommand : ICommand
{
    public string Name => "switchapi";
    public string Description => "GIF-API für /rp wechseln (Otaku / Basti / Both)";
    public bool AllowAdmin => true;

    private readonly RoleplayApi _api;

    public SwitchApiCommand(RoleplayApi api)
    {
        _api = api;
    }

    public List<SlashCommandOptionBuilder> BuildOptions() => new()
    {
        new SlashCommandOptionBuilder()
            .WithName("api")
            .WithDescription("Welche API genutzt werden soll")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true)
            .AddChoice("Otaku", "Otaku")
            .AddChoice("Basti", "Basti")
            .AddChoice("Both", "Both"),
    };

    public async Task ExecuteAsync(SocketSlashCommand cmd)
    {
        var choice = (string)cmd.Data.Options.First(o => o.Name == "api").Value;

        if (!RoleplayApi.IsBastiAvailable() && choice != "Otaku")
        {
            _api.ApiType = GifApiType.Otaku;
            await cmd.RespondAsync("Basti API is not available, defaulting to OtakuGIFs API.");
            return;
        }

        string msg;
        switch (choice)
        {
            case "Otaku":
                _api.ApiType = GifApiType.Otaku;
                msg = "Switched to OtakuGIFs API.";
                break;
            case "Basti":
                _api.ApiType = GifApiType.Basti;
                msg = "Switched to Bastiwood API.";
                break;
            case "Both":
                _api.ApiType = GifApiType.Both;
                msg = "Switched to Both APIs (random).";
                break;
            default:
                msg = "Unknown API choice.";
                break;
        }

        await cmd.RespondAsync(msg);
    }
}
