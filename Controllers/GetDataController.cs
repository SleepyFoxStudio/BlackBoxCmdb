using awesome.configurationmanagementdatabase;
using BlackBoxCmdb;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Org.BouncyCastle.Tls;
using System.Dynamic;
using System.Reflection;
using System.Text.Json;
using static GetDataController;

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
            "test" => Ok(GetTestData()),
            _ => NotFound()
        };
    }

    private object GetServerData()
    {
        List<Account> accountList = JsonSerializer.Deserialize<List<Account>>(dataService.Data);
        var tableData = new TableData();
        foreach (var account in accountList)
        {
            foreach (var serverGroup in account.ServerGroups)
            {
                Console.WriteLine(serverGroup);
                foreach (var server in serverGroup.Servers)
                {
                    var serverName = server.Tags.SingleOrDefault(s => s.Key.Equals("Name", StringComparison.InvariantCultureIgnoreCase)).Value ?? server.Id;
                    tableData.Data.Add(new {account.DataCentreType , account.AccountName,Name = serverName, server.AccountId, server.Region, ServerID = server.Id });
                }
            }
        }

        var colTitles = new List<string>();

        if (tableData.Data.Any())
        {
            var firstItem = tableData.Data.First();

            colTitles = firstItem
                .GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .ToList();
        }

        foreach (var column in colTitles)
        {
            dynamic col = new ExpandoObject();
            col.field = column;
            tableData.Columns.Add(col);
        }

        return tableData;
    }
    private object GetTestData()
    {
        var testdata = new List<TestData>
        {
            new TestData
            {
                Make = "SDL",
                Price = 202
            }
        };
        return testdata;
    }

    private object GetVolumeData()
        => new { Drive = "C:", FreeSpace = "100GB" };


    public class Server
    {
        public string Name { get; set; }
        public string PrivateIp { get; set; }
        public string AccountId { get; set; }
        public string Region { get; set; }
        public string Ec2InstanceId { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();
    }


    public class TestData
    {
        public string Make { get; set; }
        public string Model { get; set; }
        public int Price { get; set; }
    }

}