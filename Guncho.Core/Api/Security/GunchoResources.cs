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
            public const string Create = "Create";
            public const string Delete = "Delete";
            public const string EnableDisable = "EnableDisable";
            public const string Edit = "Edit";
            public const string Join = "Join";
            public const string List = "List";
            public const string Teleport = "Teleport";
            public const string View = "View";
            public const string ViewHistory = "ViewHistory";
        }

        public const string User = "User";
        public static class UserActions
        {
            public const string Create = "Create";
            public const string Edit = "Edit";
            public const string EnableDisable = "EnableDisable";
            public const string View = "View";
        }

        public const string Attribute = "Attribute";
        public static class AttributeActions
        {
            public const string Delete = "Delete";
            public const string Edit = "Edit";
            public const string View = "View";
        }

        public const string Field = "Field";
        public static class UserFields
        {
            public const string Name = "Name";
            public const string Password = "Password";
        }
        public static class RealmFields
        {
            public const string Name = "Name";
            public const string Owner = "Owner";
            public const string Acl = "Acl";
            public const string Privacy = "Privacy";
        }

        public const string Asset = "Asset";
        public static class AssetActions
        {
            public const string Create = "Create";
            public const string Delete = "Delete";
            public const string Edit = "Edit";
            public const string Import = "Import";
            public const string List = "List";
            public const string Share = "Share";
            public const string View = "View";
            public const string ViewHistory = "ViewHistory";
        }
    }
}
