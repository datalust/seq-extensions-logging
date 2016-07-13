# Seq.Extensions.Logging

This package makes it a one-liner to configure ASP.NET Core logging (_Microsoft.Extensions.Logging_) to write structured log events to [Seq](https://getseq.net).

Seq is an excellent match for the structured logging in .NET Core: for an example, see [the _dotnetconf_ deep dive session](https://channel9.msdn.com/Events/dotnetConf/2016/ASPNET-Core--deep-dive-on-building-a-real-website-with-todays-bits).

### Getting started

Add [the NuGet package](https://nuget.org/packages/seq.extensions.logging) to the `dependencies` section of your `project.json` file:

```json
  "dependencies": {
    "Seq.Extensions.Logging": "1.0.0-*"
  }
```

In your `Startup` class's `Configure()` method, call `AddSeq()` on the provided `loggerFactory`.

```csharp
  public void Configure(IApplicationBuilder app,
                        IHostingEnvironment env,
                        ILoggerFactory loggerfactory)
  {
      loggerfactory.AddSeq("http://localhost:5341");
```

The framework will inject `ILogger` instances into controllers and other classes:

```csharp
class HomeController : Controller
{
    readonly ILogger<HomeController> _log;
    
    public HomeController(ILogger<HomeController> log)
    {
        _log = log;
    }
    
    public IActionResult Index()
    {
        _log.LogInformation("Hello, world!");
    }
}
```

Log messages will be sent to Seq in batches and be visible in the Seq user interface:

(Image here).

### Additional configuration

The `AddSeq()` method exposes some basic options for controlling the connection and log volume.

| Parameter | Description | Example value |
| --------- | ----------- | ------------- |
| `apiKey` | A Seq [API key](http://docs.getseq.net/docs/api-keys) to authenticate or tag messages from the logger | `"1234567890"` |
| `levelOverrides` | | |
| `minimumLevel` | | |

### Migrating to Serilog

The simple wrapper API presented by this package does not expose all of the options supported by the underlying Serilog and Seq client libraries. Migrating to the full Serilog API however is very easy:

 1. No additional packages need to be installed
 2. Follow the instructions [here](https://github.com/serilog/serilog-extensions-logging) to change `AddSeq()` into `AddSerilog()` with a `LoggerConfiguration` object passed in
 3. Add `WriteTo.Seq()` to the Serilog configuration as per [the example](https://github.com/serilog/serilog-sinks-seq) given for the Seq sink for Serilog

Because the underlying provider is unchanged, all of the options described above are fully-supported and behave identically when using the full Serilog API.
