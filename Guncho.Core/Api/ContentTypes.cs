using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Guncho.Api
{
    // TODO: some of these can probably go
    public static class ContentTypes
    {
        // Guncho API objects
        public const string Instance = "application/vnd.guncho.instance";
        public const string Realm = "application/vnd.guncho.realm";
        //public const string Session = "application/vnd.guncho.session";
        public const string User = "application/vnd.guncho.user";

        // Assets and artifacts
        public const string PlainText = "text/plain";
        public const string Html = "text/html";
        public const string Css = "text/css";

        public const string Jpeg = "image/jpeg";
        public const string Png = "image/png";

        public const string Mp3 = "audio/mp3";
        public const string Ogg = "audio/ogg";
        public const string Aiff = "audio/aiff";
        public const string Mod = "audio/mod";

        public const string Inform6Source = "text/x-inform";
        public const string Inform7Source = "text/x-inform7";

        public const string Glulx = "application/x-glulx";
        public const string ZMachine = "application/x-zmachine";
        public const string InformDebug = "application/x-inform-debug";
        public const string Tads2 = "application/x-tads";
        public const string Tads3 = "application/x-t3vm-image";

        public const string Blorb = "application/x-blorb";
        public const string ZBlorb = "application/x-blorb;profile=\"zcode\"";
        public const string GBlorb = "application/x-blorb;profile=\"glulx\"";
    }
}
