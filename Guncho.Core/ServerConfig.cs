using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Guncho
{
    public class ServerConfig
    {
        public int GamePort { get; set; }
        public int WebPort { get; set; }
        public string CachePath { get; set; }
        public string IndexPath { get; set; }

        public string DefaultCompilerLanguage { get; set; }
        public string DefaultCompilerVersion { get; set; }
    }
}
