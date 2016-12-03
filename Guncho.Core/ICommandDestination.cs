using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Guncho
{
    public interface ICommandDestination<in TInput, TOutput>
    {
        void QueueInput(TInput line);
        Task<TOutput> SendAndGetAsync(TInput line);
    }
}