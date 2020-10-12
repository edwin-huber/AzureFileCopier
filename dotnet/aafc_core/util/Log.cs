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
        private static int FoldersPerSecond;
        private static int FilesPerSecond;

        internal static void IncrementFolderCounter()
        {
            Interlocked.Increment(ref FoldersPerSecond);
        }

        internal static void IncrementFileCounter()
        {
            Interlocked.Increment(ref FilesPerSecond);
        }

        internal static void StartPerSecondCounterThread()
        {
            ThreadStart threadStart = new ThreadStart(Log.PerSecondThreadCounter);
            Thread jobThread = new Thread(threadStart);
            jobThread.Name = "PerSecondCounter";
            jobThread.Start();
        }

        private static void PerSecondThreadCounter()
        {
            while (true)
            {
                // ToDo: Might be better to switch Exchange to before Sleep
                // and use this withj stopwatch to measure the metric, as we can't be sure when the thread 
                // will be rescheduled during high demand
                Stopwatch sw = new Stopwatch();
                sw.Start();
                int folders = 0;
                    Interlocked.Exchange(ref FoldersPerSecond, folders);
                int files = 0;
                    Interlocked.Exchange(ref FilesPerSecond, files);
                Thread.Sleep(10000);
                // could output this to the metric
                Log.Always("Approx Folder Per Second : " + FoldersPerSecond / (sw.ElapsedMilliseconds/1000));
                
                Log.Always("Approx Files Per Second : " + FilesPerSecond / (sw.ElapsedMilliseconds / 1000));
                sw.Stop();
                
            }
        }

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
            StartPerSecondCounterThread();
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
