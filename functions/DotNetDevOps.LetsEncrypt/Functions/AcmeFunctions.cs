using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Certes;
using Certes.Acme.Resource;
using Microsoft.Azure.WebJobs.Hosting;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DotNetDevOps.LetsEncrypt
{
    public class CreateOrderOrchestratorInput
    {
        public string OrderLocation { get; set; }
        public EntityId EntityId { get; set; }
        public bool UseDns01Authorization { get; set; }
        public string SignerEmail { get;  set; }
    }
    public class WaitForNotPendingInput
    {
        public string OrderLocation { get; set; }
        public EntityId EntityId { get; set; }
    }
    public static class AcmeFunctions
    {


        [FunctionName(nameof(VerifyOrderStatus))]
        public static async Task VerifyOrderStatus([ActivityTrigger] IDurableActivityContext ctx, [OrchestrationClient] IDurableOrchestrationClient starter)
        {
            try
            {
                var input = ctx.GetInput<WaitForNotPendingInput>();
                var entity = await starter.ReadEntityStateAsync<AcmeContextState>(input.EntityId);

                var context = new AcmeContext(entity.EntityState.LetsEncryptEndpoint, KeyFactory.FromPem(entity.EntityState.Pem));

                var orderCtx = context.Order(new Uri(input.OrderLocation));

                var order = await orderCtx.Resource();
                var tcs = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                while (!tcs.IsCancellationRequested && order.Status == OrderStatus.Pending)
                {
                    await Task.Delay(5000);
                    order = await orderCtx.Resource();
                }
            }catch(Exception ex)
            {
                
            }
        }

        [FunctionName(nameof(GetAuthorizationsActivity))]
        public static async Task<string[]> GetAuthorizationsActivity(
            [ActivityTrigger] IDurableActivityContext ctx,
            [OrchestrationClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            var input = ctx.GetInput<CreateOrderOrchestratorInput>();
            var entity = await starter.ReadEntityStateAsync<AcmeContextState>(input.EntityId);

            var context = new AcmeContext(entity.EntityState.LetsEncryptEndpoint, KeyFactory.FromPem(entity.EntityState.Pem));

            var orderCtx = context.Order(new Uri(input.OrderLocation));

            var order = await orderCtx.Resource();
            

            if (order.Status == Certes.Acme.Resource.OrderStatus.Invalid || order.Status == Certes.Acme.Resource.OrderStatus.Valid)
            {
                //Valid means that its been finalized and we need a new one.
                throw new Exception("Bad order state");
            }


            var authorizations = await orderCtx.Authorizations();

            // var authorizationEntiy = new EntityId("Authorization",input.EntityId.EntityKey);


            var list = new List<string>();



            if (!input.UseDns01Authorization)
            {
                foreach (var authorizationCtx in authorizations)
                {
                    var challengeContext = await authorizationCtx.Http();

                    await starter.SignalEntityAsync(new EntityId("Authorization", challengeContext.Token), nameof(AuthorizationActor.AuthorizeHttp), new AuthorizeHttpInput
                    {
                        KeyAuthz = challengeContext.KeyAuthz,
                       // Token = challengeContext.Token,
                        OrchestratorId = ctx.InstanceId,
                        AuthorizationLocation = authorizationCtx.Location,
                        EntityId = input.EntityId,
                    });
                    list.Add(challengeContext.Token);
                }
            }
            else
            {
                foreach (var authorizationCtx in authorizations)
                {
                    var challengeContext = await authorizationCtx.Dns();
                    var authorization = await authorizationCtx.Resource();
                    await starter.SignalEntityAsync(new EntityId("Authorization", challengeContext.Token), nameof(AuthorizationActor.AuthorizeDns), new AuthorizeDnsInput
                    {
                        Name = $"_acme-challenge.{authorization.Identifier.Value}",
                      //  Token = challengeContext.Token,
                        DnsTxt = context.AccountKey.DnsTxt(challengeContext.Token),
                        AuthorizationLocation = authorizationCtx.Location,
                        OrchestratorId = ctx.InstanceId,
                        EntityId = input.EntityId,
                        SignerEmail = input.SignerEmail
                    });
                    list.Add(challengeContext.Token);
                }
            }



            return list.ToArray();


            //  return order;
        }

        [FunctionName(nameof(AuthorizeOrderOrchestrator))]
        public static async Task AuthorizeOrderOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext ctx,
              [OrchestrationClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            var input = ctx.GetInput<AuthorizeOrderOrchestratorInput>();

            ctx.SetCustomStatus(new { status = "Pending" });
            //  var context = new AcmeContext(new Uri(WellKnownServers.LetsEncryptV2.AbsoluteUri),
            //  KeyFactory.FromPem(entity.EntityState.Pem));

            var tokens = await ctx.CallActivityAsync<List<string>>(nameof(GetAuthorizationsActivity), new CreateOrderOrchestratorInput
            {
                OrderLocation = input.OrderLocation,
                EntityId = input.EntityId,
                UseDns01Authorization = input.UseDns01Authorization,
                SignerEmail = input.SignerEmail
            });


            while (tokens.Any())
            {
                var token = await ctx.WaitForExternalEvent<string>("AuthorizationCompleted");

                tokens.Remove(token);
            }

            //ctx.SetCustomStatus(new { status = order.Status });

            //if (input.UseDns01Authorization && order.Status == OrderStatus.Pending)
            //{
            //    await ctx.WaitForExternalEvent("UserDnsUpdate");

            //}

            //order = await ctx.CallActivityAsync<Order>(nameof(OrderOrchestrator), new CreateOrderOrchestratorInput {
            //    LetsEncryptEndpoint = input.LetsEncryptEndpoint,
            //    OrderLocation = input.OrderLocation,
            //    EntityId = input.EntityId,
            //    OrderId = input.OrderId,
            //    Validate=true,
            //    UseDns01Authorization = input.UseDns01Authorization
            //});

            //ctx.SetCustomStatus(new { status = order.Status });

            //if(order.Status == OrderStatus.Ready)
            //{


            //}

            await ctx.CallActivityAsync(nameof(VerifyOrderStatus), new WaitForNotPendingInput
            {
                OrderLocation = input.OrderLocation,
                EntityId = input.EntityId,
            });


            await starter.RaiseEventAsync(
                input.RequestMonitorInstanceId, "Completed");


        }




    }
}
