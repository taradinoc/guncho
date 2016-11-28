using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Guncho
{
    static class FileAsync
    {
        public static async Task<string> ReadAllText(string path)
        {
            using (var rdr = new StreamReader(path))
            {
                return await rdr.ReadToEndAsync();
            }
        }

        public static async Task WriteAllText(string path, string text)
        {
            using (var wtr = new StreamWriter(path))
            {
                await wtr.WriteAsync(text);
            }
        }
    }
}
