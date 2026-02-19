namespace Afney.Cad.Domain.ECS;

/*
NE:
Component System altyapısı.

NE İÇİN:
BIM verilerini ve dinamik özellikleri esnek bir şekilde eklemek.

MİMARİ:
Composition over Inheritance.
Bir entity'ye çalışma zamanında 'HydraulicProperties' veya 'ThermalProperties' eklenebilir.
*/

public interface IComponent
{
    // Marker interface
}

public class ComponentRegistry
{
    private readonly Dictionary<Guid, List<IComponent>> _entityComponents = new();

    public void AddComponent(Guid entityId, IComponent component)
    {
        if (!_entityComponents.ContainsKey(entityId))
            _entityComponents[entityId] = new List<IComponent>();
            
        _entityComponents[entityId].Add(component);
    }
    
    public T? GetComponent<T>(Guid entityId) where T : IComponent
    {
        if (_entityComponents.TryGetValue(entityId, out var list))
        {
            return (T?)list.FirstOrDefault(c => c is T);
        }
        return default;
    }
}
