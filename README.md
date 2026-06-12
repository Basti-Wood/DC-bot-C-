# DC Bot (C#)

Ein modularer Discord-Bot in C# (.NET 8 + Discord.Net), mit MariaDB (SQL) als
Datenbank, ausgeführt über Docker / Docker Compose. C#-Port des Go-Bots
(tuubaa-bot) inkl. Level-System, Roleplay-GIFs und AI-Chat.

**Mehrere Bots, ein Code:** Jeder Bot läuft als eigener Compose-Service mit
eigenem `BOT_ID` und `DISCORD_TOKEN`. Alle SQL-Einträge sind über `BotId`
getrennt — die Bots teilen sich die Datenbank, aber niemals ihre Daten.

---

## Projektstruktur

```
DC bot C#/
├── docker-compose.yml        MariaDB + bot-main + bot-second (optional)
├── Dockerfile                Multi-stage Build (sdk → runtime)
├── .env.example              Vorlage für Secrets → nach .env kopieren
├── Program.cs                Startup: DI, DB, Client
├── DCBot.csproj              .NET Projektdatei
├── Core/                     BotService, BotConfig, GuildConfigService, Database/
├── Modules/                  Commands + Events
│   ├── Commands/             ── ALLE SLASH COMMANDS ──
│   │   ├── CommandHandler.cs   ← DER Command Handler (Discovery, Publish,
│   │   │                          Dispatch, Cooldowns, Permissions)
│   │   ├── ICommand.cs          Interface für Commands
│   │   ├── Level/    /level     ├── Ping/      /ping
│   │   ├── Top/      /top       ├── Config/    /config
│   │   ├── SetLevel/ /setlevel  ├── Cookie/    /cookie
│   │   ├── Rp/       /rp        ├── SwitchApi/ /switchapi
│   │   ├── SetGif/   /setgif    ├── ChangeAi/  /changeai
│   │   ├── LoadAi/   /loadai    ├── UnloadAi/  /unloadai
│   │   └── SetPrompt/ /setprompt
│   └── Events/               ── ALLE GATEWAY EVENTS ──
│       ├── EventManager.cs     ← DER Event Manager
│       ├── IEventListener.cs    Interface für Event-Listener
│       ├── LevelXp/             XP für Nachrichten + Level-Up-Ankündigung
│       ├── HeyLookListen/       "Hey"/"Look"/"Listen" bei Message-Burst
│       └── AiChat/              AI antwortet bei @Mention oder Reply
├── Services/                 ── GETEILTE LOGIK ──
│   ├── Level/                 XpMath, LevelRepository (SQL)
│   ├── Roleplay/              GIF-API (OtakuGIFs / Basti / Both)
│   └── Ai/                    AiService (OpenAI + Basti), Personality
├── _old/                     Alte Struktur, wird NICHT mitgebaut (löschbar)
```

Commands und Events werden beim Start **automatisch per Reflection
gefunden** — nichts muss manuell registriert werden.

---

## Ausführen

### 1. Voraussetzungen

- Docker + Docker Compose
- Ein Bot-Token: https://discord.com/developers/applications
  → Application erstellen → Bot → Token kopieren
  → **Privileged Gateway Intents aktivieren:** `SERVER MEMBERS` und `MESSAGE CONTENT`
  → Bot einladen mit Scopes `bot` + `applications.commands`

### 2. Konfigurieren

```bash
cd "D:\twitchstuff\code stuff\DC bot C#"
copy .env.example .env
```

Dann `.env` ausfüllen: `DISCORD_TOKEN_MAIN`, `DB_PASSWORD`, `DB_ROOT_PASSWORD`.
Optional: `GPT_TOKEN` (OpenAI für AI-Chat), `BASTIAPI` (Basti API für
AI + GIFs). `GUILD_ID_MAIN` setzen = Commands erscheinen sofort nur in diesem
Server (empfohlen).

### 3. Starten

```bash
docker compose up -d --build      # MariaDB + bot-main bauen und starten
docker compose logs -f bot-main   # Logs ansehen
docker compose down               # Stoppen (Daten bleiben in ./data/)
```

Nach Code-Änderungen: `docker compose up -d --build` erneut ausführen.

### 4. Zweiten Bot starten (optional)

`DISCORD_TOKEN_SECOND` in `.env` setzen (Token einer **zweiten** Discord
Application), dann:

```bash
docker compose --profile second up -d
```

Für einen dritten Bot: den `bot-second`-Block in `docker-compose.yml`
kopieren, `BOT_ID`, `container_name` und Token-Variable anpassen.

### Lokal ohne Docker (Entwicklung)

```bash
docker compose up -d mariadb
# In .env zusätzlich: DISCORD_TOKEN, BOT_ID, DB_HOST=localhost
dotnet run
```

---

## Slash Commands

| Command | Wer | Beschreibung |
|---|---|---|
| `/level [user]` | alle | Level, Rang, XP + Fortschrittsbalken |
| `/top [seite]` | alle | Rangliste (10 pro Seite) |
| `/setlevel user level` | Admin | Level direkt setzen (0–1000) |
| `/ping` | alle | Gateway- und DB-Latenz |
| `/config setchannel` | Admin | Bot-/Welcome-/Main-/Log-Channel festlegen |
| `/cookie user` | alle | Cookie 🍪 verschenken (zufällige Sprüche) |
| `/rp <reaktion> [user]` | alle | 21 Roleplay-Reaktionen mit GIF (hug, pat, slap, ...) |
| `/switchapi` | Admin | GIF-Quelle: Otaku / Basti / Both |
| `/setgif reaction url` | Admin | GIF in der Basti API speichern |
| `/changeai` | Admin | AI-Backend: OpenAI / Basti / Disabled |
| `/loadai`, `/unloadai` | Admin | Basti-Modell laden / entladen |
| `/setprompt [prompt]` | Admin | System-Prompt fürs Basti-Backend |
| `/setlistenwindow seconds` | Admin | Zeitfenster für HeyLookListen setzen (0–120 s) 0 = deaktiviert |

**XP-System:** 15–25 XP pro Nachricht, max. 1× pro 60 s, Stunden-/Tageslimit.
Kurve: `5·L² + 50·L + 100` XP pro Level, max. 1000. Level-Ups werden im
konfigurierten Bot-Channel angekündigt (`/config setchannel`).

**AI-Chat:** Standardmäßig deaktiviert. Mit `/changeai` einschalten, dann
antwortet der Bot bei @Mention oder Reply auf seine Nachrichten (Prompt max.
100 Zeichen, 5-Minuten-Gesprächsverlauf bei OpenAI, serielle Queue).

---

## Einen neuen Command hinzufügen

1. **Ordner anlegen:** `Modules/Commands/MeinBefehl/`

2. **Klasse erstellen,** die `ICommand` implementiert:

```csharp
using Discord;
using Discord.WebSocket;

namespace DCBot.Commands.MeinBefehl;

public sealed class MeinBefehlCommand : ICommand
{
    public string Name => "meinbefehl";          // lowercase!
    public string Description => "Macht etwas Tolles";
    public bool AllowEveryone => true;           // oder AllowAdmin => true
    public int Cooldown => 5;                    // Sekunden, 0 = aus

    // Per Konstruktor bekommst du alles aus der DI: DiscordSocketClient,
    // IDbContextFactory<BotDbContext>, BotConfig, GuildConfigService,
    // LevelRepository, RoleplayApi, AiService, ILogger<T>
    public MeinBefehlCommand() { }

    // Optional: Optionen/Subcommands
    public List<SlashCommandOptionBuilder> BuildOptions() => new()
    {
        new SlashCommandOptionBuilder()
            .WithName("text")
            .WithDescription("Ein Text")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true),
    };

    public async Task ExecuteAsync(SocketSlashCommand cmd)
    {
        var text = (string)cmd.Data.Options.First(o => o.Name == "text").Value;
        await cmd.RespondAsync($"Du hast gesagt: {text}");
    }
}
```

3. **Neu bauen:** `docker compose up -d --build` — der CommandHandler findet
   den Command automatisch und registriert ihn beim Ready in der Guild.

Kleinste Vorlage: `Modules/Commands/Ping/PingCommand.cs`. Mit Optionen + DB:
`Modules/Commands/Level/LevelCommand.cs`. Mit Subcommands: `Modules/Commands/Rp/RpCommand.cs`.

## Einen neuen Event-Listener hinzufügen

1. **Ordner anlegen:** `Modules/Events/MeinEvent/`
2. **Klasse erstellen,** die `IEventListener` implementiert:

```csharp
using Discord.WebSocket;

namespace DCBot.Events.MeinEvent;

public sealed class MeinEvent : IEventListener
{
    public string Name => "MeinEvent";

    public void Register(EventManager events)
    {
        events.OnMessageReceived(async msg =>
        {
            if (!msg.Author.IsBot && msg.Content == "moin")
                await msg.Channel.SendMessageAsync("moin moin");
        });
    }
}
```

Verfügbare Events: `OnMessageReceived`, `OnUserJoined`, `OnUserLeft`,
`OnReactionAdded`, `OnVoiceStateUpdated`, `OnReady`. Weitere bei Bedarf in
`Modules/Events/EventManager.cs` nach demselben Muster ergänzen (Liste +
Subscribe-Methode + eine Zeile in `Attach`).

## Eine neue Datenbank-Tabelle hinzufügen

1. Entity in `Core/Database/Entities/` anlegen — **immer mit `BotId`**
   (und meist `GuildId`), damit die Bot-Trennung erhalten bleibt.
2. `DbSet` + Index in `Core/Database/BotDbContext.cs` eintragen.
3. Achtung: `EnsureCreated` erstellt nur **neue** Datenbanken, keine neuen
   Tabellen in bestehenden. Tabelle einmal manuell per SQL anlegen, oder
   (löscht alle Daten!) `./data/mariadb` leeren — oder auf EF Core
   Migrations umsteigen (`dotnet ef migrations add ...` + `Migrate()`).

---

## In die Datenbank schauen

```bash
docker exec -it dcbot-mariadb mariadb -u dcbot -p dcbot
# Passwort = DB_PASSWORD aus .env

SHOW TABLES;
SELECT * FROM user_levels ORDER BY Xp DESC LIMIT 10;
SELECT * FROM guild_configs;
```

Jede Zeile hat eine `BotId`-Spalte (`main`, `second`, ...) — daran siehst du,
welcher Bot den Eintrag besitzt.

---

## Troubleshooting

- **Keine Slash Commands sichtbar** → `GUILD_ID_MAIN` in `.env` setzen
  (sofortige Registrierung) und Bot neu starten. Global registrierte
  Commands brauchen bis zu 1 Stunde.
- **„Privileged intent provided is not enabled"** → Im Developer Portal
  unter Bot die Intents `SERVER MEMBERS` und `MESSAGE CONTENT` aktivieren.
- **AI antwortet nicht** → AI ist standardmäßig aus: `/changeai` nutzen.
  Für OpenAI muss `GPT_TOKEN` gesetzt sein, für Basti `BASTIAPI` +
  `/loadai`.
- **DB-Verbindungsfehler beim ersten Start** → Normal, der Bot wartet mit
  Retry, bis MariaDB bereit ist.
- **Daten komplett zurücksetzen** → `docker compose down` und den Ordner
  `./data/mariadb` löschen.
- **`_old/` Ordner** → Backup der ersten Projektversion, wird nicht
  mitkompiliert. Kann gelöscht werden.
