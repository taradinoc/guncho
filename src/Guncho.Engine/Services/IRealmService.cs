using System.Collections.Generic;
using System.Threading.Tasks;

namespace Guncho.Services
{
    /// <summary>
    /// Service for managing game realms.
    /// </summary>
    public interface IRealmService
    {
        Realm? GetRealm(string name);
        Task<Realm?> GetRealmAsync(string name);
        Task SaveRealmAsync(Realm realm);
        Task<Realm?> CreateRealmAsync(Player owner, string name, RealmFactory factory);
        Task<bool> DeleteRealmAsync(Realm realm);
        IEnumerable<Realm> GetAllRealms();
        IEnumerable<RealmFactory> GetRealmFactories();
        Task<RealmEditingOutcome> UpdateRealmSourceAsync(Realm realm);
    }
}
