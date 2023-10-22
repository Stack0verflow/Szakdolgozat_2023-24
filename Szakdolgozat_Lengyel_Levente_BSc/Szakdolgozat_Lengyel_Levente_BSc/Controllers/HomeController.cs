using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Szakdolgozat_Lengyel_Levente_BSc.Models;

namespace Szakdolgozat_Lengyel_Levente_BSc.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Page1()
    {
        return View();
    }
    
    public IActionResult Page2()
    {
        return View();
    }
    
    public IActionResult Info()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}