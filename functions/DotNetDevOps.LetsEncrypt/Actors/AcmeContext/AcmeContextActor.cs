using Certes;
using Certes.Acme.Resource;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace DotNetDevOps.LetsEncrypt
{

    /// <summary>
    /// Actors for functions
    /// Operations support return values of void, Task, Task<T>
    /// OperationAttribute specifies OperationName and input type if needed. Deserialized when running and injected as parameter
    /// DI on constructor and operation methods for additional services/values.
    /// State is saved after each operation
    /// </summary>
    [ActorService(Name = "AcmeContext")]
    public class AcmeContextActor : Actor<AcmeContextState>
    {
        private readonly IDurableOrchestrationClient starter;

        public AcmeContextActor(IDurableOrchestrationClient starter) 
        {
            this.starter = starter;           
        }

        [Operation(nameof(UpdateDNS), Input = typeof(UpdateDNSInput))]
        public void UpdateDNS(UpdateDNSInput updateDNSInput, ILogger log)
        {  
            log.LogWarning("Please update Domain:\n\n domain={domain}\n dnsTxt={dnsTxt}\n location={location}\n updateUrl=http://localhost:7071/providers/DotNetDevOps.Letsencrypt/certificates/{instanceId}",updateDNSInput.Name, updateDNSInput.DnsTxt, updateDNSInput.Location, updateDNSInput.MonitorInstanceId);
            
        }

        [Operation(nameof(Initialize))]
        public async Task Initialize(AcmeContextInitializeInput input)
        { 
            if (State.SignerEmail != input.SignerEmail)
            {
                var context = new AcmeContext(input.LetsEncryptEndpoint);

                var tos = context.TermsOfService();

                var account = await context.NewAccount(input.SignerEmail, true);

                await account.Update(
                    contact: new[] { $"mailto:{input.SignerEmail}" },
                    agreeTermsOfService: true);


                var pem = context.AccountKey.ToPem();
                //  var ms = new MemoryStream(Encoding.ASCII.GetBytes(pem));
                State.SignerEmail = input.SignerEmail;
                State.Pem = pem;
                State.LetsEncryptEndpoint = input.LetsEncryptEndpoint;
            }
        }

        [Operation(nameof(FinalizeOrder))]
        public async Task<FinalizeOutput> FinalizeOrder(FinalizeInput input, ILogger logger)
        {
            var context = new AcmeContext(input.LetsEncryptEndpoint, KeyFactory.FromPem(State.Pem));
            var orderId = string.Join("", input.Domains).ToMD5Hash();
            var orderCtx = context.Order(new Uri(State.Orders[orderId]));
            var order = await orderCtx.Resource();

            if (order.Status == OrderStatus.Ready)
            {
                var certKey = KeyFactory.NewKey(KeyAlgorithm.ES256);

                order = await orderCtx.Finalize(
                      input.CsrInfo, certKey);

                if (order.Status == Certes.Acme.Resource.OrderStatus.Invalid)
                {                    
                    throw new Exception(order?.Error?.ToString() ?? $"{orderCtx} is invalid");
                }


                var certChain = await orderCtx.Download();
                var pfx = certChain.ToPfx(certKey).Build("CN="+input.Domains.First(), "");
                var cert = new X509Certificate2(pfx);
                return new FinalizeOutput {  Pfx =pfx, Thumbprint = cert.Thumbprint};

            }

            throw new Exception(order?.Error?.ToString() ?? $"{orderCtx} is invalid");
        }

        [Operation(nameof(CreateOrder))]
        public async Task CreateOrder(OrderInput input, ILogger logger)
        {
            logger.LogInformation("Creating a new order for {domains}", input.Domains);
            using (logger.BeginScope(new Dictionary<string, string> { ["Domains"]= string.Join(",",input.Domains) }))
            {

                var context = new AcmeContext(State.LetsEncryptEndpoint,
                    KeyFactory.FromPem(State.Pem));

                {
                    var accountCtx = await context.Account();
                    var account = await accountCtx.Resource();
                    logger.LogInformation("Account context loaded for {contact} - {status}", string.Join(", ", account.Contact), account.Status);

                    logger.BeginScope(new Dictionary<string, string> { ["Contact"] = string.Join(", ", account.Contact) });

                    var ordersCtx = await accountCtx.Orders();
                    var orders = await ordersCtx.Orders();


                    foreach (var orderCtx in orders)
                    {
                        var order = await orderCtx.Resource();
                        logger.LogInformation("Account had existing order: {OrderIdentifier} | Status={Status} NotBefore={NotBefore} NotAfter={NotAfter} Raw={Raw}",
                            string.Join(", ", order.Identifiers.Select(o => $"{o.Type}={o.Value}")), order.Status, order.NotBefore, order.NotAfter, JsonConvert.SerializeObject(order));

                    }
                }


                {


                    var orderCtx = await context.NewOrder(input.Domains);
                    var order = await orderCtx.Resource();

                    logger.BeginScope(new Dictionary<string, string> { ["OrderIdentifier"] = string.Join(", ", order.Identifiers.Select(o => $"{o.Type}={o.Value}"))});

                    logger.LogInformation("Created order Status={Status} NotBefore={NotBefore} NotAfter={NotAfter} Raw={Raw}",
                            order.Status, order.NotBefore, order.NotAfter, JsonConvert.SerializeObject(order));

                   

                    if (order.Status == Certes.Acme.Resource.OrderStatus.Invalid || order.Status == Certes.Acme.Resource.OrderStatus.Valid)
                    {
                        // Old order thats finished or broken.
                    }
                    else
                    {


                        var orderId = string.Join("", input.Domains).ToMD5Hash();

                        var status = await starter.GetStatusAsync(orderId);
                        logger.LogInformation("Authorization Process Status={Status} CustomStatus={CustomStatus}", status?.RuntimeStatus.ToString()??"NotRunning", status?.CustomStatus?.ToString());
                        //  if (status == null)
                        {

                            logger.LogInformation("Starting Authorization Process");

                            await starter.StartNewAsync(nameof(AcmeFunctions.AuthorizeOrderOrchestrator), orderId,
                                new AuthorizeOrderOrchestratorInput
                                {
                                    OrderLocation = orderCtx.Location.AbsoluteUri,
                                    EntityId = Id,
                                    OrderId = orderId,
                                    LetsEncryptEndpoint = State.LetsEncryptEndpoint,
                                    RequestMonitorInstanceId = input.MonitorInstanceId
                                });

                            State.Orders[orderId] = orderCtx.Location.AbsoluteUri;



                        }
                        //else
                        //{
                           
                        //}
                    }
                }
            }
        }
    }
}
