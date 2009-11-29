using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
using System.Web;
using System.Web.Security;
using System.Web.SessionState;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using Guncho;

namespace ControlPanel
{
    internal static class Common
    {
        public static IController GetController(HttpSessionState session)
        {
            return GetController(session, false);
        }

        public static IController GetController(HttpSessionState session, bool allowLoggedOut)
        {
            ControllerFactory fac = (ControllerFactory)Activator.GetObject(
                typeof(ControllerFactory),
                Properties.Settings.Default.ServerControllerURL);
            return fac.GetController();
        }

        public static KeyValuePair<string, string>[] GetRealmFactories(IController controller,
            out string defaultKey)
        {
            defaultKey = null;

            string[] keys = controller.ListRealmFactories();
            List<KeyValuePair<string, string>> result = new List<KeyValuePair<string, string>>(keys.Length);

            foreach (string key in keys)
            {
                string value;

                if (key.Length == 4 && char.IsDigit(key[0]) && char.IsLetter(key[1]) &&
                    char.IsDigit(key[2]) && char.IsDigit(key[3]))
                {
                    value = "Inform 7 (build " + key + ")";
                    if (defaultKey == null || key.CompareTo(defaultKey) < 0)
                        defaultKey = key;
                }
                else
                {
                    value = key;
                }

                result.Add(new KeyValuePair<string, string>(key, value));
            }

            result.Sort((a, b) => a.Value.CompareTo(b.Value));
            return result.ToArray();
        }

        public static string EncodeAsID(string text)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text);
            string b64 = Convert.ToBase64String(bytes);
            b64 = b64.Replace('+', '-');
            b64 = b64.Replace('/', '.');
            b64 = b64.Replace('=', '_');
            return b64;
        }
    }
}
