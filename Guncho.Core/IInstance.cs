using System;
using System.Threading.Tasks;

namespace Guncho
{
    public interface IInstance : IDisposable, IPlayerDestination<string>, ICommandDestination<string, string>
    {
        bool IsActive { get; }
        string Name { get; }
        bool RawMode { get; set; }
        Realm Realm { get; }
        bool RestartRequested { get; set; }
        DateTime WatchdogTime { get; }

        Task Activate();
        Task Deactivate();
        Task PolitelyDispose();
    }
}