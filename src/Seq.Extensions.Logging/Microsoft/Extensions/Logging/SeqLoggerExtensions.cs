using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Serilog.Core;
using Seq.Extensions.Logging;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Sinks.Seq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Configuration;
using Serilog.Sinks.PeriodicBatching;
// ReSharper disable UnusedMember.Global

namespace Microsoft.Extensions.Logging;

/// <summary>
/// Extends <see cref="ILoggerFactory"/> with methods for configuring Seq logging.
/// </summary>
public static class SeqLoggerExtensions
{
    const string LocalServerUrl = "http://localhost:5341";

    /// <summary>
    /// Adds a Seq logger configured from the supplied configuration section.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="configuration">A configuration section with details of the Seq server connection.</param>
    /// <returns>A logger factory to allow further configuration.</returns>
    public static ILoggerFactory AddSeq(this ILoggerFactory loggerFactory, IConfigurationSection configuration)
    {
        if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        if (TryCreateProvider(configuration, LogLevel.Information, [], out var provider))
            loggerFactory.AddProvider(provider);

        return loggerFactory;
    }

    /// <summary>
    /// Adds a Seq logger.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="serverUrl">The Seq server URL; the default is http://localhost:5341.</param>
    /// <param name="apiKey">A Seq API key to authenticate or tag messages from the logger.</param>
    /// <param name="minimumLevel">The level below which events will be suppressed (the default is <see cref="LogLevel.Information"/>).</param>
    /// <param name="levelOverrides">A dictionary mapping logger name prefixes to minimum logging levels.</param>
    /// <param name="enrichers">A collection of enrichers to apply.</param>
    /// <returns>A logger factory to allow further configuration.</returns>
    public static ILoggerFactory AddSeq(
        this ILoggerFactory loggerFactory,
        string serverUrl = LocalServerUrl,
        string? apiKey = null,
        LogLevel minimumLevel = LogLevel.Information,
        IDictionary<string, LogLevel>? levelOverrides = null,
        IEnumerable<Action<EnrichingEvent>>? enrichers = null)
    {
        if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
        if (serverUrl == null) throw new ArgumentNullException(nameof(serverUrl));

        var provider = CreateProvider(serverUrl, apiKey, minimumLevel, levelOverrides, enrichers);
        loggerFactory.AddProvider(provider);
        return loggerFactory;
    }

    /// <summary>
    /// Adds a Seq logger configured from the supplied configuration section.
    /// </summary>
    /// <param name="loggingBuilder">The logging builder.</param>
    /// <param name="configuration">A configuration section with details of the Seq server connection.</param>
    /// <returns>A logging builder to allow further configuration.</returns>
    public static ILoggingBuilder AddSeq(
        this ILoggingBuilder loggingBuilder,
        IConfigurationSection configuration)
    {
        if (loggingBuilder == null) throw new ArgumentNullException(nameof(loggingBuilder));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        if (TryCreateProvider(configuration, LevelAlias.Minimum, Array.Empty<Action<EnrichingEvent>>(), out var provider))
            loggingBuilder.Services.AddSingleton<ILoggerProvider>(_ => provider);

        return loggingBuilder;
    }

    /// <summary>
    /// Adds a Seq logger.
    /// </summary>
    /// <param name="loggingBuilder">The logging builder.</param>
    /// <param name="serverUrl">The Seq server URL; the default is http://localhost:5341.</param>
    /// <param name="apiKey">A Seq API key to authenticate or tag messages from the logger.</param>
    /// <param name="enrichers">A collection of enrichers to apply.</param>
    /// <returns>A logging builder to allow further configuration.</returns>
    public static ILoggingBuilder AddSeq(
        this ILoggingBuilder loggingBuilder,
        string serverUrl = LocalServerUrl,
        string? apiKey = null,
        IEnumerable<Action<EnrichingEvent>>? enrichers = null)
    {
        if (loggingBuilder == null) throw new ArgumentNullException(nameof(loggingBuilder));
        if (serverUrl == null) throw new ArgumentNullException(nameof(serverUrl));

        loggingBuilder.Services.AddSingleton<ILoggerProvider>(s =>
        {
            var opts = s.GetService<ILoggerProviderConfiguration<SerilogLoggerProvider>>();
            var provider = CreateProvider(opts?.Configuration, serverUrl, apiKey, enrichers);
            return provider;
        });

        return loggingBuilder;
    }

    static bool TryCreateProvider(
        IConfigurationSection configuration,
        LogLevel defaultMinimumLevel,
        IEnumerable<Action<EnrichingEvent>> enrichers,
        [NotNullWhen(true)] out SerilogLoggerProvider? provider)
    {
        var serverUrl = configuration["ServerUrl"];
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            SelfLog.WriteLine("Unable to add the Seq logger: no ServerUrl was present in the configuration");
            provider = null;
            return false;
        }

        var apiKey = configuration["ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = null;

        var minimumLevel = defaultMinimumLevel;
        var levelSetting = configuration["MinimumLevel"];
        if (!string.IsNullOrWhiteSpace(levelSetting))
        {
            if (!Enum.TryParse(levelSetting, out minimumLevel))
            {
                SelfLog.WriteLine("The minimum level setting `{0}` is invalid", levelSetting);
                minimumLevel = LogLevel.Information;
            }
        }

        var levelOverrides = new Dictionary<string, LogLevel>();
        foreach (var levelOverride in configuration.GetSection("LevelOverride").GetChildren())
        {
            if (!Enum.TryParse(levelOverride.Value, out LogLevel value))
            {
                SelfLog.WriteLine("The level override setting `{0}` for `{1}` is invalid", levelOverride.Value, levelOverride.Key);
                continue;
            }

            levelOverrides[levelOverride.Key] = value;
        }

        provider = CreateProvider(serverUrl, apiKey, minimumLevel, levelOverrides, enrichers);
        return true;
    }

    static SerilogLoggerProvider CreateProvider(
        IConfiguration? configuration,
        string? defaultServerUrl,
        string? defaultApiKey,
        IEnumerable<Action<EnrichingEvent>>? enrichers)
    {
        string? serverUrl = null, apiKey = null;
        if (configuration != null)
        {
            serverUrl = configuration["ServerUrl"];
            apiKey = configuration["ApiKey"];
        }

        if (string.IsNullOrWhiteSpace(serverUrl))
            serverUrl = defaultServerUrl;

        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = defaultApiKey;

        return CreateProvider(serverUrl, apiKey, LevelAlias.Minimum, null, enrichers);
    }

    static SerilogLoggerProvider CreateProvider(
        string? serverUrl,
        string? apiKey,
        LogLevel minimumLevel,
        IDictionary<string, LogLevel>? levelOverrides,
        IEnumerable<Action<EnrichingEvent>>? enrichers)
    {
        var levelSwitch = new LoggingLevelSwitch(minimumLevel);

        var sink = new SeqSink(
            serverUrl!,
            apiKey,
            256 * 1024,
            new ControlledLevelSwitch(levelSwitch),
            null);

        LevelOverrideMap? overrideMap = null;
        if (levelOverrides != null && levelOverrides.Count != 0)
        {
            var overrides = new Dictionary<string, LoggingLevelSwitch>();
            foreach (var levelOverride in levelOverrides)
            {
                overrides[levelOverride.Key] = new LoggingLevelSwitch(levelOverride.Value);
            }

            overrideMap = new LevelOverrideMap(overrides, levelSwitch);
        }

        var batchingSink = new PeriodicBatchingSink(sink, new PeriodicBatchingSinkOptions
        {
            BatchSizeLimit = 1000,
            Period = TimeSpan.FromSeconds(2),
        });

        var logger = new Logger(batchingSink, new Enricher(enrichers ?? []), batchingSink.Dispose, levelSwitch, overrideMap);
        var provider = new SerilogLoggerProvider(logger);
        return provider;
    }
}