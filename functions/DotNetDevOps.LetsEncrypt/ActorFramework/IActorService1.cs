using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using System.Linq.Expressions;



namespace DotNetDevOps.LetsEncrypt
{
    public interface IActorService<T>
    {
        string Name { get; }
        Task CallAsync<TInput>(IDurableOrchestrationContext ctx, string key, Expression<Func<T, Func<TInput, Task>>> ex, TInput input);
        Task<TOut> CallAsync<TInput, TOut>(IDurableOrchestrationContext ctx, string key, Expression<Func<T, Func<TInput, Task<TOut>>>> ex, TInput input);
    }
}
