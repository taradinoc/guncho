using System.Collections.Generic;
using System.Threading.Tasks;

namespace Guncho.Services
{
    /// <summary>
    /// Service for managing game instances.
    /// </summary>
    public interface IInstanceService
    {
        IInstance? GetInstance(string name);
        Task<IInstance> GetDefaultInstanceAsync(Realm realm);
        Task EnterInstanceAsync(Player player, IInstance instance, string? savedPosition = null);
        IDictionary<Player, IInstance> GetPlayerInstances();
    }
}
