using Serilog.Core;
using Serilog.Core.Enrichers;
using Serilog.Events;

namespace Seq.Extensions.Logging;

class Enricher: ILogEventEnricher
{
    readonly ILogEventEnricher _builtIn = new SafeAggregateEnricher([new ExceptionDataEnricher()]);

    public Enricher(IEnumerable<Action<EnrichingEvent>>? enrichers = null)
    {
        _enrichers = (enrichers ?? Array.Empty<Action<EnrichingEvent>>()).ToArray();
    }

    readonly Action<EnrichingEvent>[] _enrichers;

    public void Enrich(LogEvent logEvent, ILogEventPropertyValueFactory propertyFactory)
    {
        _builtIn.Enrich(logEvent, propertyFactory);

        var enriching = new EnrichingEvent(logEvent, propertyFactory);

        foreach (var enricher in _enrichers)
        {
            try
            {
                enricher(enriching);
            }
            catch (Exception ex)
            {
                SelfLog.WriteLine("Exception {0} caught while enriching {1}.", ex, logEvent);
            }
        }
    }
}
