using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Certes;
using Certes.Acme.Resource;
using Microsoft.Azure.WebJobs.Hosting;



namespace DotNetDevOps.LetsEncrypt
{
    public class CreateOrderOrchestratorInput
    {
        public Uri LetsEncryptEndpoint { get; set; }
        public string OrderLocation { get;  set; }
        public EntityId EntityId { get;  set; }
        public string OrderId { get;  set; }
        public bool Validate { get; set; }
    }
    public static class AcmeFunctions
    {

        [FunctionName(nameof(OrderOrchestrator))]
        public static async Task<Order> OrderOrchestrator([ActivityTrigger] IDurableActivityContext ctx, [OrchestrationClient] IDurableOrchestrationClient starter, ILogger log)
        {
            var input = ctx.GetInput<CreateOrderOrchestratorInput>();
            var entity = await starter.ReadEntityStateAsync<AcmeContextState>(input.EntityId);

            var context = new AcmeContext(input.LetsEncryptEndpoint, KeyFactory.FromPem(entity.EntityState.Pem));
           
            var orderCtx = context.Order(new Uri(input.OrderLocation));

            var order = await orderCtx.Resource();

            if (order.Status == Certes.Acme.Resource.OrderStatus.Invalid || order.Status == Certes.Acme.Resource.OrderStatus.Valid)
            {
                //Valid means that its been finalized and we need a new one.
                // await ordersService.ClearOrderAsync(domain);
            }            
            else
            {
                var authorizations = await orderCtx.Authorizations();


                foreach (var authorizationCtx in authorizations)
                {
                 
                    var dnsChallenge = await authorizationCtx.Dns();
                   
                    if (input.Validate)
                    {
                        var ch = await dnsChallenge.Validate();

                        while (ch.Status == Certes.Acme.Resource.ChallengeStatus.Pending)
                        {
                            // Wait for ACME server to validate the identifier
                            await Task.Delay(3000);
                            ch = await dnsChallenge.Resource();
                        }


                        

                    }
                    else {
                        var authorization = await authorizationCtx.Resource();
                        var dnsTxt = context.AccountKey.DnsTxt(dnsChallenge.Token);

                        await starter.SignalEntityAsync(input.EntityId, nameof(AcmeContextActor.UpdateDNS), new UpdateDNSInput
                        {
                            Name= $"_acme-challenge.{authorization.Identifier.Value}",
                            DnsTxt = dnsTxt,
                            OrderId = input.OrderId,
                            Location = authorizationCtx.Location.AbsoluteUri,
                            MonitorInstanceId = ctx.InstanceId
                        });

                    }

                }

                if (input.Validate)
                {
                    order = await orderCtx.Resource();

                   
                }

            }

            return order;
        }

        [FunctionName(nameof(AuthorizeOrderOrchestrator))]
        public static async Task AuthorizeOrderOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext ctx,
              [OrchestrationClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            var input = ctx.GetInput<AuthorizeOrderOrchestratorInput>();

            ctx.SetCustomStatus(new { status = "Pending"});
            //  var context = new AcmeContext(new Uri(WellKnownServers.LetsEncryptV2.AbsoluteUri),
            //  KeyFactory.FromPem(entity.EntityState.Pem));

            var order = await ctx.CallActivityAsync<Order>(nameof(OrderOrchestrator), new CreateOrderOrchestratorInput {
                LetsEncryptEndpoint = input.LetsEncryptEndpoint,
                OrderLocation = input.OrderLocation,
                EntityId=input.EntityId,
                OrderId = input.OrderId
            });

            ctx.SetCustomStatus(new { status = order.Status });

            if (order.Status == OrderStatus.Pending)
            {
                await ctx.WaitForExternalEvent("UserDnsUpdate");

            }

            order = await ctx.CallActivityAsync<Order>(nameof(OrderOrchestrator), new CreateOrderOrchestratorInput {
                LetsEncryptEndpoint = input.LetsEncryptEndpoint,
                OrderLocation = input.OrderLocation,
                EntityId = input.EntityId,
                OrderId = input.OrderId,
                Validate=true
            });

            ctx.SetCustomStatus(new { status = order.Status });

            if(order.Status == OrderStatus.Ready)
            {

             
            }


            await starter.RaiseEventAsync(
                input.RequestMonitorInstanceId, "Completed",order.Status.Value);

            
        }




    }
}
