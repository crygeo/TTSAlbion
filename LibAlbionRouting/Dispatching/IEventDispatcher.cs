// LibAlbionRouting/Dispatching/IAlbionEventDispatcher.cs

using LibAlbionProtocol.Models;
using LibNetWork.Interfaces;

namespace LibAlbionRouting.Dispatching;

/// <summary>
/// Despachador puro: mapea códigos de evento a handlers sin lógica de negocio.
/// Agnóstico de dominio: solo "esto pasó, aquí están los parámetros crudos".
/// </summary>
public interface IEventDispatcher : IPacketHandler
{
    /// <summary>
    /// Suscribe un handler a un evento específico de Albion.
    /// El dispatcher no sabe qué hace el handler, solo lo invoca.
    /// </summary>
    void Subscribe<TModel>(EventCodes code, Func<TModel, Task> handler) 
        where TModel : class;
}