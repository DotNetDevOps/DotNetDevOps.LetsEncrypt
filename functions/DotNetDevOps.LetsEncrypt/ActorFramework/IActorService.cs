using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;



namespace DotNetDevOps.LetsEncrypt
{
    public interface IActorService
    {
        string Name { get; }
        Task ExecuteAsync<TInput, TOutput, TState>(IDurableEntityContext ctx);

    }
}
