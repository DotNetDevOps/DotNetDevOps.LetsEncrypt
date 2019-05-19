using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Azure.Management.AppService.Fluent.Models;

[assembly: WebJobsStartup(typeof(DotNetDevOps.LetsEncrypt.GeneratedFunction))]

//GENERATED - DO NOT MODIFY

namespace DotNetDevOps.LetsEncrypt
{
      
    public class GeneratedFunction : IWebJobsStartup
    {
        public static Dictionary<string, Type> TypeMappings = new Dictionary<string, Type>()
        {
            ["AcmeContext"] = typeof(AcmeContextActor),
            ["AzureWebsite"] = typeof(AzureWebsiteActor),
            ["Authorization"] = typeof(AuthorizationActor)
        };

        public void Configure(IWebJobsBuilder builder)
        {
            foreach(var type in TypeMappings)
            {
                builder.Services.AddTransient(type.Value);
            }
           
        }

        [FunctionName("AcmeContext")]
        public static async Task AcmeContextEntity(
           [EntityTrigger] IDurableEntityContext ctx,
           [ActorService(Name ="AcmeContext")] IActorService actorservice,
           [OrchestrationClient] IDurableOrchestrationClient starter)
        {
            switch (ctx.OperationName)
            {
                case "Initialize":
                     await actorservice.ExecuteAsync<AcmeContextInitializeInput,object, AcmeContextState>(ctx);
                    break;
                case "CreateOrder":
                    await actorservice.ExecuteAsync<OrderInput,object, AcmeContextState>(ctx);
                    break;
                case "FinalizeOrder":
                    await actorservice.ExecuteAsync<FinalizeInput, FinalizeOutput, AcmeContextState>(ctx);
                    break;
                case "ValidateAuthorization":
                    await actorservice.ExecuteAsync<ValidateAuthorizationInput, object, AcmeContextState>(ctx);
                    break;
                default:
                    throw new InvalidOperationException(ctx.OperationName + " is not known");

            }
           
        }

        [FunctionName("Authorization")]
        public static async Task AuthorizationEntity(
         [EntityTrigger] IDurableEntityContext ctx,
         [ActorService(Name = "Authorization")] IActorService actorservice,
         [OrchestrationClient] IDurableOrchestrationClient starter)
        {
            switch (ctx.OperationName)
            {
                case "AuthorizeHttp":
                    await actorservice.ExecuteAsync<AuthorizeHttpInput, object, AuthorizationActorState>(ctx);
                    break;
                case "AuthorizeDns":
                    await actorservice.ExecuteAsync<AuthorizeDnsInput, object, AuthorizationActorState>(ctx);
                    break;
                case "AuthorizationCompleted":
                    await actorservice.ExecuteAsync<object, object, AuthorizationActorState>(ctx);
                    break;
                default:
                    throw new InvalidOperationException(ctx.OperationName + " is not known");

            }

        }

        [FunctionName("AzureWebsite")]
        public static async Task AzureWebsiteEntity(
          [EntityTrigger] IDurableEntityContext ctx,
          [ActorService(Name = "AzureWebsite")] IActorService actorservice,
          [OrchestrationClient] IDurableOrchestrationClient starter)
        {
            switch (ctx.OperationName)
            {
                case "Load":
                    await actorservice.ExecuteAsync<LoadWebsiteInput, SiteInner, AzureWebsiteActorState>(ctx);
                    break;
                case "UpdateCertificate":
                    await actorservice.ExecuteAsync<UpdateCertificateInput, object, AzureWebsiteActorState>(ctx);
                    break;

                default:
                    throw new InvalidOperationException(ctx.OperationName + " is not known");

            }

        }
    }
}
