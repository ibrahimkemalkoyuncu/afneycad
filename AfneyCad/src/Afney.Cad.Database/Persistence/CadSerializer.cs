using System.Text.Json;
using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Domain.Tables;

namespace Afney.Cad.Database.Persistence;

public class ProjectData
{
    public List<CadEntity> Entities { get; set; } = new();
    public List<CadLayer> Layers { get; set; } = new();
}

public class CadSerializer
{
    public string Serialize(CadDatabase database)
    {
        return Serialize(new ProjectData
        {
            Entities = database.GetAllEntities().ToList(),
            Layers = database.GetLayers().ToList()
        });
    }

    public string Serialize(ProjectData data)
    {
        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            IncludeFields = true
        };
        
        return JsonSerializer.Serialize(data, options);
    }

    public ProjectData Deserialize(string json)
    {
        var options = new JsonSerializerOptions 
        { 
            IncludeFields = true 
        };
        return JsonSerializer.Deserialize<ProjectData>(json, options) ?? new ProjectData();
    }
}
