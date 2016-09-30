using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Seq.Extensions.Logging.Seq.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Debugging;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// Extends <see cref="ILoggerFactory"/> with methods for configuring Seq logging.
    /// </summary>
    public static class SeqLoggerExtensions
    {
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

            var serverUrl = configuration["ServerUrl"];
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                SelfLog.WriteLine("Unable to add the Seq logger: no ServerUrl was present in the configuration");
                return loggerFactory;
            }

            var apiKey = configuration["ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                apiKey = null;

            var minimumLevel = LogLevel.Information;
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

            return loggerFactory.AddSeq(serverUrl, apiKey, minimumLevel, levelOverrides);
        }

        /// <summary>
        /// Adds a Seq logger configured from the supplied configuration section.
        /// </summary>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="serverUrl"></param>
        /// <param name="apiKey">A Seq API key to authenticate or tag messages from the logger.</param>
        /// <param name="minimumLevel">The level below which events will be suppressed (the default is <see cref="LogLevel.Information"/>).</param>
        /// <param name="levelOverrides">A dictionary mapping logger name prefixes to minimum logging levels.</param>
        /// <returns>A logger factory to allow further configuration.</returns>
        public static ILoggerFactory AddSeq(
            this ILoggerFactory loggerFactory,
            string serverUrl,
            string apiKey = null,
            LogLevel minimumLevel = LogLevel.Information,
            IDictionary<string, LogLevel> levelOverrides = null)
        {
            var levelSwitch = new LoggingLevelSwitch(Conversions.MicrosoftToSerilogLevel(minimumLevel));

            var configuration = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
                .Enrich.FromLogContext()
                .WriteTo.Seq(serverUrl, apiKey: apiKey, controlLevelSwitch: levelSwitch);

            foreach (var levelOverride in levelOverrides)
            {
                configuration.MinimumLevel.Override(levelOverride.Key, Conversions.MicrosoftToSerilogLevel(levelOverride.Value));
            }

            var logger = configuration.CreateLogger();
            return loggerFactory.AddSerilog(logger, dispose: true);
        }
    }
}
