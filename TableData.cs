using System.Dynamic;

internal class TableData
{

    public List<ExpandoObject> Columns { get; internal set; } = [];
    public List<Dictionary<string, object>> Data { get; internal set; } =[];
    public required string ViewName { get; set; }
}