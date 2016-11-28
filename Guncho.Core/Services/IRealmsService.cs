using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Guncho.Services
{
    [ContractClass(typeof(IRealmsServiceContract))]
    public interface IRealmsService
    {
        IEnumerable<Realm> GetAllRealms();
        Realm GetRealmByName(string name);
        Task<Realm> CreateRealm(Player owner, string name, RealmFactory factory);
        Task<RealmEditingOutcome> UpdateRealmSourceAsync(Realm realm, Stream bodyStream);

        IEnumerable<RealmFactory> GetRealmFactories();

        bool IsValidNameChange(string oldName, string newName);

        Task<bool> TransactionalUpdate(Realm realm, Func<Realm, bool> transaction);
    }

    [ContractClassFor(typeof(IRealmsService))]
    abstract class IRealmsServiceContract : IRealmsService
    {
        public IEnumerable<Realm> GetAllRealms()
        {
            Contract.Ensures(Contract.Result<IEnumerable<Realm>>() != null);
            return default(IEnumerable<Realm>);
        }

        public Realm GetRealmByName(string name)
        {
            Contract.Requires(name != null);
            return default(Realm);
        }

        public Task<Realm> CreateRealm(Player player, string name, RealmFactory factory)
        {
            Contract.Requires(player != null);
            Contract.Requires(!string.IsNullOrWhiteSpace(name));
            Contract.Requires(factory != null);
            /*Contract.Ensures
                (Contract.Result<Realm>() == null ||
                (Contract.Result<Realm>().Factory == factory && Contract.Result<Realm>().Name == name));*/
            return default(Task<Realm>);
        }

        public IEnumerable<RealmFactory> GetRealmFactories()
        {
            Contract.Ensures(Contract.Result<IEnumerable<RealmFactory>>() != null);
            return default(IEnumerable<RealmFactory>);
        }

        public Task<RealmEditingOutcome> UpdateRealmSourceAsync(Realm realm, Stream bodyStream)
        {
            Contract.Requires(realm != null);
            Contract.Requires(bodyStream != null);
            Contract.Ensures(Contract.Result<Task<RealmEditingOutcome>>() != null);
            return default(Task<RealmEditingOutcome>);
        }

        public bool IsValidNameChange(string oldName, string newName)
        {
            Contract.Requires(oldName != null);
            Contract.Requires(newName != null);
            return default(bool);
        }

        public Task<bool> TransactionalUpdate(Realm realm, Func<Realm, bool> transaction)
        {
            Contract.Requires(realm != null);
            Contract.Requires(transaction != null);
            return default(Task<bool>);
        }
    }
}
