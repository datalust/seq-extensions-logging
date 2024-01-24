using Serilog.Sinks.Seq;
using Tests.Support;
using Xunit;

namespace Seq.Extensions.Logging.Tests.Serilog.Sinks.Seq
{
    public class SeqPayloadFormatterTests
    {
        [Fact]
        public void JsonSafeStringPropertiesAreIncludedAsIs()
        {
            const string json = "{\"A\": 42}";
            var evt = Some.LogEvent("The answer is {Answer}", new JsonSafeString(json));
            var payload = SeqPayloadFormatter.FormatCompactPayload(new[] { evt }, null);
            Assert.Contains("\"Answer\":{\"A\": 42}", payload);
        }

        [Fact]
        public void DefaultJsonSafeStringsDoNotCorruptPayload()
        {
            var evt = Some.LogEvent("The answer is {Answer}", (JsonSafeString)default);
            var payload = SeqPayloadFormatter.FormatCompactPayload(new[] { evt }, null);
            Assert.Contains("\"Answer\":\"<null>\"", payload);
        }
    }
}