using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace WebApplication.Controllers
{
    public class HomeController : Controller
    {
        readonly ILogger<HomeController> _log;

        public HomeController(ILogger<HomeController> log)
        {
            _log = log;
        }

        public IActionResult Index()
        {
            _log.LogInformation("Hello, {Name}!", "world");
            _log.LogDebug("Hello, again!");

            using (_log.BeginScope("Example"))
            using (_log.BeginScope(42))
            using (_log.BeginScope("Process {OrderId}", 12345))
            using (_log.BeginScope(new Dictionary<string, object> { ["MessageId"] = 100 }))
            {
                _log.LogInformation("Events in this block have additional properties attached");
            }

            return View();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
