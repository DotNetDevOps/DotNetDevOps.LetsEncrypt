using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using System.Linq.Expressions;



namespace DotNetDevOps.LetsEncrypt
{
    public interface IActorProxy{

        Task CallAsync<T, TInput>(IDurableOrchestrationContext ctx, string key, Expression<Func<T, Func<TInput, Task>>> ex, TInput input);

        Task<TOut> CallAsync<T, TInput, TOut>(IDurableOrchestrationContext ctx, string key, Expression<Func<T, Func<TInput, Task<TOut>>>> ex, TInput input);
       

    }
}
