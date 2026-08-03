using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PmcWh.Web.Models;

namespace PmcWh.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        var model = new DashboardViewModel
        {
            Cards = new List<DashboardCard>
            {
                new() { Title = "Users", Value = "1,280", Icon = "people", Color = "primary" },
                new() { Title = "Orders", Value = "3,420", Icon = "shopping_cart", Color = "success" },
                new() { Title = "Revenue", Value = "$84K", Icon = "attach_money", Color = "warning" },
                new() { Title = "Alerts", Value = "12", Icon = "notifications", Color = "danger" }
            }
        };

        return View(model);
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
