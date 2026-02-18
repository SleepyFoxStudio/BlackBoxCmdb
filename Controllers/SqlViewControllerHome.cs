using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlackBoxCmdb.Controllers;

[Authorize]
public class SqlViewController() : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}