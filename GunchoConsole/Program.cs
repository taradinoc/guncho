using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Guncho;

namespace Guncho.ConsoleDaemon
{
    class Program
    {
        static void Main(string[] args)
        {
            ServerRunner runner = new ServerRunner();
            runner.Logger = new ConsoleLogger();
            runner.Run();
        }
    }
}
