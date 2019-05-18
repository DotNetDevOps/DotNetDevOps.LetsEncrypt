using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Management.AppService.Fluent.Models;
using System.Net.Http;
using System.Linq;
using Microsoft.Azure.WebJobs.Hosting;
using Newtonsoft.Json;

namespace DotNetDevOps.LetsEncrypt
{
    public static class CertificateFunctions
    {


        //[FunctionName(nameof(GetOrderStatus))]
        //public static async Task GetOrderStatus(
        //    [ActivityTrigger] IDurableActivityContext ctx,
        //            [OrchestrationClient] IDurableOrchestrationClient starter,
        //    [ActorService(Name = "AcmeContext")] IActorProxy actorProxy, ILogger log)
        //{

        //    var request = ctx.GetInput<AddCertificateRequest>();

        //    await starter.SignalEntityAsync(new EntityId("AcmeContext", request.SignerEmail.ToMD5Hash()), nameof(AcmeContextActor.Initialize), new AcmeContextInitializeInput { SignerEmail = request.SignerEmail, LetsEncryptEndpoint = request.LetsEncryptEndpoint });
        //    await starter.SignalEntityAsync(new EntityId("AcmeContext", request.SignerEmail.ToMD5Hash()), nameof(AcmeContextActor.CreateOrder), new OrderInput { Domains = request.Domains, MonitorInstanceId = ctx.InstanceId });


        //}

        [FunctionName(nameof(AddCertificateOrchestrator))]
        public static async Task AddCertificateOrchestrator(
        [OrchestrationTrigger] IDurableOrchestrationContext ctx,
        [OrchestrationClient] IDurableOrchestrationClient starter,
        [ActorService(Name = "AcmeContext")] IActorProxy actorProxy,
        ILogger log)
        {
            var input = ctx.GetInput<AddCertificateRequest>();
            ctx.SetCustomStatus(new { status = "Pending" });

            // var site = await ctx.CallActivityAsync<SiteInner>(nameof(AzureResourceManagerFunctions.GetSite), (request.SubscriptionId, request.ResourceGroupName, request.SiteName, request.SlotName));

            var websiteId = new EntityId("AzureWebsite", string.Join("", input.ResourceGroupName, input.SiteName, input.SlotName, input.SubscriptionId).ToMD5Hash());
            var arcmeId = new EntityId("AcmeContext", input.SignerEmail.ToMD5Hash());

            var site = await ctx.CallEntityAsync<SiteInner>(websiteId, nameof(AzureWebsiteActor.Load),
                new LoadWebsiteInput
                {
                    ResourceGroupname = input.ResourceGroupName,
                    SiteName = input.SiteName,
                    SlotName = input.SlotName,
                    SubscriptionId = input.SubscriptionId,
                    Domains = input.Domains
                });

            ctx.SetCustomStatus(new { status = "SiteLoaded" });

            await ctx.CallEntityAsync(arcmeId, nameof(AcmeContextActor.Initialize),
                new AcmeContextInitializeInput { SignerEmail = input.SignerEmail, LetsEncryptEndpoint = input.LetsEncryptEndpoint });

            ctx.SetCustomStatus(new { status = "AcmeInitialized" });

            await ctx.CallEntityAsync(arcmeId, nameof(AcmeContextActor.CreateOrder),
                new OrderInput { Domains = input.Domains, MonitorInstanceId = ctx.InstanceId });

            await ctx.WaitForExternalEvent("Completed");

            ctx.SetCustomStatus(new { status = "OrderCreated" });

            var pfx = await ctx.CallEntityAsync<FinalizeOutput>(arcmeId, nameof(AcmeContextActor.FinalizeOrder),
                new FinalizeInput { CsrInfo = input.CsrInfo, LetsEncryptEndpoint = input.LetsEncryptEndpoint, Domains = input.Domains });

            ctx.SetCustomStatus(new { status = "OrderFinalized" });


            await ctx.CallEntityAsync<SiteInner>(websiteId, nameof(AzureWebsiteActor.UpdateCertificate), new UpdateCertificateInput { Pfx = pfx, Domains = input.Domains, UseIpBasedSsl = input.UseIpBasedSsl });

            ctx.SetCustomStatus(new { status = "CertificateUpdated" });
 
        }

        [FunctionName("CreateCertificateRequest")]
        public static async Task<HttpResponseMessage> Run(
         [HttpTrigger(AuthorizationLevel.Function, "post", Route = "providers/DotNetDevOps.Letsencrypt/certificates")] HttpRequestMessage req,
         [OrchestrationClient] IDurableOrchestrationClient starter,
         ILogger log)
        {
            var addCertificateRequest = await req.Content.ReadAsAsync<AddCertificateRequest>();

            var instanceId = await starter.StartNewAsync(nameof(AddCertificateOrchestrator), addCertificateRequest);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return await starter.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, TimeSpan.FromSeconds(3));
        }

        [FunctionName("Updated")]
        public static async Task userUpdated(
       [HttpTrigger(AuthorizationLevel.Function, "get", Route = "providers/DotNetDevOps.Letsencrypt/certificates/{instanceId}")] HttpRequestMessage req,
       [OrchestrationClient] IDurableOrchestrationClient starter, string instanceId,
       ILogger log)
        {
            await starter.RaiseEventAsync(instanceId, "UserDnsUpdate");
        }

    }
}
