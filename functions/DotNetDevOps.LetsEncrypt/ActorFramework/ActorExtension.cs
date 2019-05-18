using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs.Hosting;

[assembly: WebJobsStartup(typeof(DotNetDevOps.LetsEncrypt.ActorExtensionWebJobsStartUp))]



namespace DotNetDevOps.LetsEncrypt
{
    public class ActorExtension : IExtensionConfigProvider
    {
        private readonly IServiceProvider serviceProvider;

        public ActorExtension(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }
        public void Initialize(ExtensionConfigContext context)
        {
            var rule = context.AddBindingRule<ActorServiceAttribute>();


            // rule.AddOpenConverter<IActorService<OpenType>, IActorService<OpenType>>(typeof(CustomConverter<>));
            rule.AddConverter<IActorService, IActorProxy>(o=>o as IActorProxy);
            rule.BindToInput(Factory);
           // rule.BindToInput<IActorProxy>(Factory3);
        }

        private Task<IActorProxy> Factory3(ActorServiceAttribute arg1, ValueBindingContext arg2)
        {
            return Task.FromResult(new ActorServiceBroadcaster(arg1.Name, serviceProvider.GetService<IServiceScopeFactory>()) as IActorProxy);
        }

       

        private Task<IActorService> Factory(ActorServiceAttribute arg1, ValueBindingContext arg2)
        {

            return Task.FromResult(new ActorServiceBroadcaster(arg1.Name,serviceProvider.GetService<IServiceScopeFactory>()) as IActorService);

            //    arg2.FunctionContext.MethodName
            // return serviceProvider.GetService(typeof(IAspNetCoreRunner<>).MakeGenericType(typeof()))
        }
    }
}
