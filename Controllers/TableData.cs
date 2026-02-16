using System.Dynamic;

internal class TableData
{

    public List<ExpandoObject> Columns { get; internal set; } = [];
    public List<object> Data { get; internal set; } =[];
}