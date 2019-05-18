using Microsoft.Azure.WebJobs;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;



namespace DotNetDevOps.LetsEncrypt
{
    public class ActorExtensionWebJobsStartUp : IWebJobsStartup
    {
        public void Configure(IWebJobsBuilder builder)
        {
            builder.AddExtension<ActorExtension>();
           

            builder.Services.AddSingleton<IDurableOrchestrationClient>(sp =>
            {
                var configurations = sp.GetServices<IExtensionConfigProvider>();

                var durableTaskExtension = configurations.OfType<DurableTaskExtension>().FirstOrDefault();

                var getclient = durableTaskExtension.GetType().GetMethod("GetClient", BindingFlags.NonPublic | BindingFlags.Instance);
                var orchestrationClientAttribute = new OrchestrationClientAttribute();



                var client = getclient.Invoke(durableTaskExtension, new object[] { orchestrationClientAttribute });

                return client as IDurableOrchestrationClient;
            });

        }
    }
}
