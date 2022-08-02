using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WebExample.Models;

namespace WebExample.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        _logger.LogInformation("Hello, {Name}!", "world");
        _logger.LogDebug("Hello, again!");

        using (_logger.BeginScope("Example"))
        using (_logger.BeginScope(42))
        using (_logger.BeginScope("Process {OrderId}", 12345))
        using (_logger.BeginScope(new Dictionary<string, object> { ["MessageId"] = 100 }))
        {
            _logger.LogInformation("Events in this block have additional properties attached");
        }
        
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
