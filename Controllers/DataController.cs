using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlackBoxCmdb.Controllers;

[Authorize]
public class DataController(DataService dataService) : Controller
{
    public IActionResult Index()
    {
        var userName = User.Identity?.Name;
        ViewData["UserName"] = userName;
        ViewData["DataJson"] = JsonSerializer.Serialize(dataService.Data, new JsonSerializerOptions { WriteIndented = true });
        return View();
    }
}