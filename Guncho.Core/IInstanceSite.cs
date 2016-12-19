using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Guncho
{
    public interface IInstanceSite
    {
        void NotifyInstanceFinished(IInstance instance, Player[] abandoned, bool wasTerminated);
        Task SetEventIntervalAsync(IInstance instance, int value);

        Task<bool> FlushPlayerAsync(Player p);
        Task<bool> SendLineToPlayerAsync(Player p);
        Task<bool> SendToPlayerAsync(Player p, char c);
        Task<bool> SendToPlayerAsync(Player p, string s);

        void TransferPlayer(Player p, string spec);

        TimeSpan? GetPlayerIdleTime(Player queriedPlayer);
    }
}
