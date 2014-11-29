using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Guncho.Api.Security
{
    public static class GunchoResources
    {
        public const string Realm = "Realm";
        public static class RealmActions
        {
            public const string EditAssets = "EditAssets";
            public const string ListRealm = "ListRealm";
            public const string ViewAssets = "ViewAssets";
            public const string ViewDetails = "ViewDetails";
        }

        public const string User = "User";
        public static class UserActions
        {
            public const string EditProfile = "EditProfile";
        }
    }
}
