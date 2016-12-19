using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Guncho.Services;

namespace Guncho.Api.Tests
{
    internal class FakeRealmsService : IRealmsService
    {
        private readonly List<Realm> realms = new List<Realm>();

        public void Add(string realmName, string ownerName)
        {
            //XXX
        }

        public Task<Realm> CreateRealmAsync(Player owner, string name, RealmFactory factory)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Realm> GetAllRealms()
        {
            throw new NotImplementedException();
        }

        public Realm GetRealmByName(string name)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<RealmFactory> GetRealmFactories()
        {
            throw new NotImplementedException();
        }

        public bool IsValidNameChange(string oldName, string newName)
        {
            throw new NotImplementedException();
        }

        public Task<bool> TransactionalUpdateAsync(Realm realm, Func<Realm, bool> transaction)
        {
            throw new NotImplementedException();
        }

        public Task<RealmEditingOutcome> UpdateRealmSourceAsync(Realm realm, Stream bodyStream)
        {
            throw new NotImplementedException();
        }
    }
}