using awesome.configurationmanagementdatabase;
using BlackBoxCmdb;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Org.BouncyCastle.Tls;
using System.Data.SqlClient;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using static SqlViewController;





[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SqlViewController(DataService dataService) : ControllerBase
{
    [HttpGet()]
    [HttpPost()]
    public async Task<IActionResult> GetData([FromBody] DataRequest request)
    {
        var view = string.IsNullOrEmpty(request.View) ? "Accounts" : request.View;
        request.View = view;
        await ValidateViewName(view);
        return Ok(GetSqlData(request));
    }

    private async Task ValidateViewName(string view)
    {
        var names = new List<string>();

        // Change to type = "view" if you want views
        var sql = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";

        var conn = dataService.PrimaryConnection;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = sql;
            //conn.Open();

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    names.Add(reader.GetString(0));
                }
            }
        }

        if (!names.Any(s=> s.Equals(view)))
        {
            throw new Exception("Invalid view name set");
        }
    }

    public class DataRequest
    {
        public string? View { get; set; }
    }

    private object GetSqlData(DataRequest request)
    {
        var tableData = new TableData();
        tableData.ViewName = request.View;

        var connection = dataService.PrimaryConnection;

        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {request.View};";

        using var reader = command.ExecuteReader();

        int fieldCount = reader.FieldCount;
        string[] columnNames = new string[fieldCount];

        // --- 2️⃣ Capture column names once ---
        for (int i = 0; i < fieldCount; i++)
        {
            columnNames[i] = reader.GetName(i);
            dynamic col = new ExpandoObject();
            col.field = reader.GetName(i);
            col.width = 200;
            tableData.Columns.Add(col);
        }

        // --- 3️⃣ Pre-size list if possible ---
        var dataList = new List<Dictionary<string, object>>(1000); // adjust initial capacity as needed

        while (reader.Read())
        {
            var row = new Dictionary<string, object>(fieldCount);
            for (int i = 0; i < fieldCount; i++)
            {
                row[columnNames[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            dataList.Add(row);
        }

        tableData.Data = dataList;

        return tableData;
    }
}