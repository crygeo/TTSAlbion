using RequipAlbion.Network.Handler;

namespace TTSAlbion.Albion.Handler;

public abstract class GenericRouterBase<TEnum> : IGenericRouter<TEnum> where TEnum : Enum
{
    // Diccionario que mapea un OperationCode a
    // una lista de handlers async
    private readonly Dictionary<TEnum, (Type ModelType, Delegate Handler)> _routes = new();


    /// <summary>
    /// Suscribe un handler para un OperationCode específico.
    /// </summary>
    public void Subscribe<TModel>(TEnum code, Action<TModel> handler)
    {
        _routes[code] = (typeof(TModel), handler);
    }

    /// <summary>
    /// Enruta el response al handler correspondiente.
    /// </summary>
    public bool TryRoute(TEnum code, IReadOnlyDictionary<byte, object> parameters)
    {
        if (!_routes.TryGetValue(code, out var route))
            return false;

        try
        {
            // Crear instancia del modelo usando el constructor que recibe parameters
            var model = Activator.CreateInstance(route.ModelType, parameters);

            // Invocar handler
            route.Handler.DynamicInvoke(model);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Router] Error en la ruta {code}: {ex}");
            return false;
        }

    }

}