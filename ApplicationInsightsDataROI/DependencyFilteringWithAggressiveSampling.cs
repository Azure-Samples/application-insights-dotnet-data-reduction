namespace ApplicationInsightsDataROI
{
    using System;
    using System.Collections.Concurrent;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;

    internal class DependencyFilteringWithAggressiveSampling : ITelemetryProcessor
    {
        private readonly ITelemetryProcessor next;
        private readonly AdaptiveSamplingTelemetryProcessor samplingProcessor;

        private readonly ConcurrentDictionary<string, Tuple<Metric, Metric>> metrics = new ConcurrentDictionary<string, Tuple<Metric, Metric>>();

        public DependencyFilteringWithAggressiveSampling(ITelemetryProcessor next, TelemetryConfiguration configuration)
        {
            this.next = next;
            this.samplingProcessor = new AdaptiveSamplingTelemetryProcessor(this.next)
            {
                ExcludedTypes = "Event", // exclude custom events from being sampled
                MaxTelemetryItemsPerSecond = 1, // default: 5 calls/sec
                SamplingPercentageIncreaseTimeout = TimeSpan.FromSeconds(1), // default: 2 min
                SamplingPercentageDecreaseTimeout = TimeSpan.FromSeconds(1), // default: 30 sec
                EvaluationInterval = TimeSpan.FromSeconds(1), // default: 15 sec
                InitialSamplingPercentage = 25, // default: 100%
            };
        }

        public void Process(ITelemetry item)
        {
            // check telemetry type
            if (item is DependencyTelemetry)
            {
                var d = item as DependencyTelemetry;

                if (d.Duration < TimeSpan.FromMilliseconds(100))
                {
                    this.samplingProcessor.Process(item);
                    return;
                }
            }

            this.next.Process(item);
        }
    }
}
