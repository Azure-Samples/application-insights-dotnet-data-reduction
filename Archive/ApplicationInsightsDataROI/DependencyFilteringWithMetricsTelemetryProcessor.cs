namespace ApplicationInsightsDataROI
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;

    internal class DependencyFilteringWithMetricsTelemetryProcessor : ITelemetryProcessor, IDisposable
    {
        private readonly ITelemetryProcessor next;
        private readonly ConcurrentDictionary<string, Tuple<Metric, Metric>> metrics = new ConcurrentDictionary<string, Tuple<Metric, Metric>>();
        private readonly MetricManager manager;

        public DependencyFilteringWithMetricsTelemetryProcessor(ITelemetryProcessor next, TelemetryConfiguration configuration)
        {
            this.next = next;
            this.manager = new MetricManager(new TelemetryClient(configuration));
        }

        public void Process(ITelemetry item)
        {
            // check telemetry type
            if (item is DependencyTelemetry)
            {
                var d = item as DependencyTelemetry;

                // increment counters
                var metrics = this.metrics.GetOrAdd(d.Type, (type) =>
                    {
                        var numberOfDependencies = this.manager.CreateMetric("# of dependencies", new Dictionary<string, string> { { "type", type } });
                        var dependenciesDuration = this.manager.CreateMetric("dependencies duration (ms)", new Dictionary<string, string> { { "type", type } });
                        return new Tuple<Metric, Metric>(numberOfDependencies, dependenciesDuration);
                    });

                metrics.Item1.Track(1);
                metrics.Item2.Track(d.Duration.TotalMilliseconds);

                if (d.Duration < TimeSpan.FromMilliseconds(100))
                {
                    // if dependency duration > 100 ms then stop telemetry
                    // processing and return from the pipeline
                    return;
                }
            }

            this.next.Process(item);
        }

        public void Dispose()
        {
            this.manager.Dispose();
        }
    }
}
