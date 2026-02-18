using awesome.configurationmanagementdatabase;
using BlackBoxCmdb;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Org.BouncyCastle.Tls;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using static GetDataController;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GetDataController(DataService dataService) : ControllerBase
{
    [HttpGet("server")]
    [HttpPost("server")]
    public IActionResult GetData([FromBody] DataRequest request)
    {
        return Ok(GetServerData(request));
    }

    public class DataRequest
    {
        public List<string> IncludeColumns { get; set; } = new List<string>();
    }

    private object GetServerData(DataRequest request)
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
                    var baseData = new Dictionary<string, object>
                    {
                        ["DataCentreType"] = account.DataCentreType,
                        ["AccountName"] = account.AccountName,
                        ["Name"] = serverName,
                        ["AccountId"] = server.AccountId,
                        ["Region"] = server.Region,
                        ["ServerID"] = server.Id
                    };

                    // Start with a list of rows to add
                    var dataItems = new List<Dictionary<string, object>>();

                    if (request.IncludeColumns.Contains("Volume") && server.Volumes.Any())
                    {
                        // Create one row per volume
                        foreach (var volume in server.Volumes)
                        {
                            // Copy base data
                            var row = new Dictionary<string, object>(baseData)
                            {
                                ["VolumeId"] = volume.Id,
                                ["VolumeIops"] = volume.Iops,
                                ["VolumeLabel"] = volume.Label,
                                ["VolumeCreated"] = volume.Created,
                                ["VolumeSize"] = volume.Size,
                                ["VolumeType"] = volume.Type
                            };

                            dataItems.Add(row);
                        }
                    }
                    else
                    {
                        // No volumes, just add the base row
                        dataItems.Add(new Dictionary<string, object>(baseData));
                    }

                    // Add to your table
                    tableData.Data.AddRange(dataItems);

                }
            }
        }

        var colTitles = new List<string>();

        if (tableData.Data.Any())
        {
            colTitles = tableData.Data
                .SelectMany(row => row.Keys)  // flatten all keys from all rows
                .Distinct()                   // remove duplicates
                .ToList();
        }

        foreach (var column in colTitles)
        {
            dynamic col = new ExpandoObject();
            col.field = column;
            col.width = 200;
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