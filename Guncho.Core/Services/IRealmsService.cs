using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Guncho.Services
{
    public interface IRealmsService
    {
        IEnumerable<Realm> GetAllRealms();
        IEnumerable<RealmFactory> GetRealmFactories();
    }
}
