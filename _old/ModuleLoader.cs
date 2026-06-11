using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DCBot.Core;

/// <summary>
/// Finds every class implementing IModule in this assembly, creates it via
/// dependency injection (so modules can request DbContextFactory, the
/// Discord client, BotConfig, loggers, ...) and calls Register() on it.
/// </summary>
public static class ModuleLoader
{
    public static IReadOnlyList<IModule> LoadAll(
        IServiceProvider services,
        CommandManager commands,
        EventManager events,
        ILogger logger)
    {
        var moduleTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => typeof(IModule).IsAssignableFrom(t)
                        && t is { IsClass: true, IsAbstract: false })
            .OrderBy(t => t.Name)
            .ToList();

        var modules = new List<IModule>();

        foreach (var type in moduleTypes)
        {
            try
            {
                var module = (IModule)ActivatorUtilities.CreateInstance(services, type);
                module.Register(commands, events);
                modules.Add(module);
                logger.LogInformation("Loaded module: {Name}", module.Name);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load module {Type}", type.FullName);
            }
        }

        return modules;
    }
}
