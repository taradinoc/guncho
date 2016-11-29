using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Guncho
{
    static class ForEach
    {
        public static Task Async<T>(IEnumerable<T> items, Func<T, Task> body)
        {
            return Task.WhenAll(from i in items select body(i));
        }
    }
}
