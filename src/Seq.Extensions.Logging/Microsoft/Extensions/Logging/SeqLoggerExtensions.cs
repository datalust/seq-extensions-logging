using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Serilog.Core;
using Seq.Extensions.Logging;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Sinks.Seq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Configuration;
using Serilog.Sinks.PeriodicBatching;

namespace Microsoft.Extensions.Logging
{
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

            if (TryCreateProvider(configuration, LogLevel.Information, out var provider))
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
        /// <returns>A logger factory to allow further configuration.</returns>
        public static ILoggerFactory AddSeq(
            this ILoggerFactory loggerFactory,
            string serverUrl = LocalServerUrl,
            string apiKey = null,
            LogLevel minimumLevel = LogLevel.Information,
            IDictionary<string, LogLevel> levelOverrides = null)
        {
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
            if (serverUrl == null) throw new ArgumentNullException(nameof(serverUrl));

            var provider = CreateProvider(serverUrl, apiKey, minimumLevel, levelOverrides);
            loggerFactory.AddProvider(provider);
            return loggerFactory;
        }

        /// <summary>
        /// Adds a Seq logger.
        /// </summary>
        /// <param name="loggingBuilder">The logging builder.</param>
        /// <param name="serverUrl">The Seq server URL; the default is http://localhost:5341.</param>
        /// <param name="apiKey">A Seq API key to authenticate or tag messages from the logger.</param>
        /// <returns>A logging builder to allow further configuration.</returns>
        public static ILoggingBuilder AddSeq(
            this ILoggingBuilder loggingBuilder,
            string serverUrl = LocalServerUrl,
            string apiKey = null)
        {
            if (loggingBuilder == null) throw new ArgumentNullException(nameof(loggingBuilder));
            if (serverUrl == null) throw new ArgumentNullException(nameof(serverUrl));

            loggingBuilder.Services.AddSingleton<ILoggerProvider>(s =>
            {
                var opts = s.GetService<ILoggerProviderConfiguration<SerilogLoggerProvider>>();
                var provider = CreateProvider(opts?.Configuration, serverUrl, apiKey);
                return provider; 
            });

            return loggingBuilder;
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

            if (TryCreateProvider(configuration, LevelAlias.Minimum, out var provider))
                loggingBuilder.Services.AddSingleton<ILoggerProvider>(_ => provider);

            return loggingBuilder;
        }

        static bool TryCreateProvider(
            IConfigurationSection configuration,
            LogLevel defaultMinimumLevel,
            out SerilogLoggerProvider provider)
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
            foreach (var overr in configuration.GetSection("LevelOverride").GetChildren())
            {
                LogLevel value;
                if (!Enum.TryParse(overr.Value, out value))
                {
                    SelfLog.WriteLine("The level override setting `{0}` for `{1}` is invalid", overr.Value, overr.Key);
                    continue;
                }

                levelOverrides[overr.Key] = value;
            }

            provider = CreateProvider(serverUrl, apiKey, minimumLevel, levelOverrides);
            return true;
        }

        static SerilogLoggerProvider CreateProvider(
            IConfiguration configuration,
            string defaultServerUrl,
            string defaultApiKey)
        {
            string serverUrl = null, apiKey = null;
            if (configuration != null)
            {
                serverUrl = configuration["ServerUrl"];
                apiKey = configuration["ApiKey"];
            }

            if (string.IsNullOrWhiteSpace(serverUrl))
                serverUrl = defaultServerUrl;

            if (string.IsNullOrWhiteSpace(apiKey))
                apiKey = defaultApiKey;

            return CreateProvider(serverUrl, apiKey, LevelAlias.Minimum, null);
        }

        static SerilogLoggerProvider CreateProvider(
            string serverUrl,
            string apiKey,
            LogLevel minimumLevel,
            IDictionary<string, LogLevel> levelOverrides)
        {
            var levelSwitch = new LoggingLevelSwitch(minimumLevel);

            var sink = new SeqSink(
                serverUrl,
                apiKey,
                256 * 1024,
                new ControlledLevelSwitch(levelSwitch),
                null);

            LevelOverrideMap overrideMap = null;
            if (levelOverrides != null && levelOverrides.Count != 0)
            {
                var overrides = new Dictionary<string, LoggingLevelSwitch>();
                foreach (var levelOverride in levelOverrides)
                {
                    overrides.Add(levelOverride.Key, new LoggingLevelSwitch(levelOverride.Value));
                }

                overrideMap = new LevelOverrideMap(overrides, levelSwitch);
            }

            var batchingSink = new PeriodicBatchingSink(sink, new PeriodicBatchingSinkOptions
            {
                BatchSizeLimit = 1000,
                Period = TimeSpan.FromSeconds(2),
            });
            
            var logger = new Logger(levelSwitch, batchingSink, batchingSink.Dispose, overrideMap);
            var provider = new SerilogLoggerProvider(logger);
            return provider;
        }
    }
}
