using aafccore.resources;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace aafccore.util
{
    /// <summary>
    /// App Insights or Log4J type integration can go here.
    /// </summary>
    internal static class Log
    {
        // ToDo: Change to proper singleton initialization rather than static constructor
        static Log()
        {
            // Create the DI container.
            IServiceCollection services = new ServiceCollection();
            services.AddLogging(loggingBuilder => loggingBuilder.AddFilter("Microsoft", LogLevel.Warning)
                   .AddFilter("System", LogLevel.Warning)
                   .AddFilter("aafc_core.Program", LogLevel.Debug)
                   .AddFilter<Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider>("Category", LogLevel.Information)
                   .AddConsole());
            services.AddApplicationInsightsTelemetryWorkerService(CopierConfiguration.Config.GetValue<string>(ConfigStrings.InstrumentationKey));
            // Build ServiceProvider.
            IServiceProvider serviceProvider = services.BuildServiceProvider();
            // Obtain logger instance from DI.
            logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            // Obtain TelemetryClient instance from DI, for additional manual tracking or to flush.
            telemetryClient = serviceProvider.GetRequiredService<TelemetryClient>();
        }

        static readonly ILogger<Program> logger;
        static readonly TelemetryClient telemetryClient;
        internal static bool QuietMode { get; set; } = false;

        internal static void TrackMetric(string metricId, double metricValue)
        {
            var metric = telemetryClient.GetMetric(metricId);
            metric.TrackValue(metricValue);
        }

        internal static void TrackEvent(string eventName)
        {
            telemetryClient.TrackEvent(eventName);
        }

        internal static void Always(string message)
        {
            if (!QuietMode)
            {
                logger.LogInformation(CreateDateString() + FixedStrings.LogInfoSeparator + "{\"" + message + "\"}}");
            }
        }

        [Conditional("DEBUG")]
        public static void Debug(string message, string thread)
        {
            if (!QuietMode)
            {
                logger.LogDebug(CreateDateString() + FixedStrings.LogDebugSeparator + "{\"thread\":\"" + thread + "\"," + "{\"" + message + "\"}}");
            }
        }

        private static string CreateDateString()
        {
            return DateTime.UtcNow.ToShortDateString() + FixedStrings.LogBlankSeparator + DateTime.UtcNow.ToLongTimeString();
        }
    }
}
