using BlackBoxCmdb;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GetDataController(DataService dataService) : ControllerBase
{
    [HttpGet("{type}")]
    public IActionResult GetData(string type)
    {
        return type?.ToLower() switch
        {
            "server" => Ok(GetServerData()),
            "volume" => Ok(GetVolumeData()),
            _ => NotFound()
        };
    }

    private object GetServerData()
    {
        var servers = JsonSerializer.Serialize(dataService.Data, new JsonSerializerOptions { WriteIndented = true });
        return servers;
    }

    private object GetVolumeData()
        => new { Drive = "C:", FreeSpace = "100GB" };
}