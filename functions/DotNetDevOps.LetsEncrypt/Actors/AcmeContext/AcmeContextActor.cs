using Certes;
using Certes.Acme.Resource;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetDevOps.LetsEncrypt
{
    public class AuthorizeHttpInput
    {
        public string KeyAuthz { get;  set; }
        public string Token { get;  set; }
        public string OrchestratorId { get;  set; }
        public Uri AuthorizationLocation { get;  set; }
        public EntityId EntityId { get;  set; }
    }
    public class AuthorizeDnsInput
    {
        public string Name { get; set; }
        public string DnsTxt { get; set; }
        public string OrchestratorId { get;  set; }
        public Uri AuthorizationLocation { get;  set; }
       // public string Token { get;  set; }
        public EntityId EntityId { get; set; }
    }

    public class AuthorizationActorState{
       // public Dictionary<string, string> Authorizations { get; set; } = new Dictionary<string, string>();
       public string KeyAuthz { get; set; }
        public string OrchestratorId { get; set; }
        public EntityId EntityId { get;  set; }
        public Uri AuthorizationLocation { get; set; }
        public List<DnsAuthorizationRequest> DnsAuthorizationRequests { get; set; }
    }

    public class DnsAuthorizationRequest
    {
        public string Name { get;  set; }
        public string Value { get;  set; }
        public string RecordType { get;  set; }
        public string Callback { get;  set; }
    }

    [ActorService(Name = "Authorization")]
    public class AuthorizationActor : Actor<AuthorizationActorState>
    {
        private readonly IDurableOrchestrationClient starter;

        public AuthorizationActor(IDurableOrchestrationClient starter)
        {
            this.starter = starter;
        }


        [Operation(nameof(AuthorizeHttp), Input = typeof(AuthorizeHttpInput))]
        public async Task AuthorizeHttp(AuthorizeHttpInput input, ILogger log)
        {
            this.State.KeyAuthz = input.KeyAuthz;
            this.State.EntityId = input.EntityId;
            this.State.AuthorizationLocation = input.AuthorizationLocation;

            this.SaveState();

            await starter.SignalEntityAsync(input.EntityId, nameof(AcmeContextActor.ValidateAuthorization), new ValidateAuthorizationInput { AuthorizationLocation = input.AuthorizationLocation, UseDns = false, OrchestratorId = input.OrchestratorId });

        }

        [Operation(nameof(AuthorizeDns), Input = typeof(AuthorizeDnsInput))]
        public void AuthorizeDns(AuthorizeDnsInput input, ILogger log)
        {
            this.State.OrchestratorId = input.OrchestratorId;
            this.State.EntityId = input.EntityId;
            this.State.AuthorizationLocation = input.AuthorizationLocation;

            if(this.State.DnsAuthorizationRequests == null)
            {
                this.State.DnsAuthorizationRequests = new List<DnsAuthorizationRequest>();
            }

            var givethisToExternalService = new DnsAuthorizationRequest
            {
                Name = input.Name,
                Value = input.DnsTxt,
                RecordType = "TXT",
                Callback = $"https://letsencrypt-provider/providers/DotNetDevOps.Letsencrypt/challenges/{Id.EntityKey}"
            };
            this.State.DnsAuthorizationRequests.Add(givethisToExternalService);

            log.LogWarning("Please update Domain:\n\n domain={domain}\n dnsTxt={dnsTxt}\n callback={callback}", input.Name, input.DnsTxt, $"http://localhost:7071/providers/DotNetDevOps.Letsencrypt/challenges/{Id.EntityKey}");
            //location={location}\n updateUrl=
            this.SaveState();
            //TODO call external service/callback 

         

        }

        [Operation(nameof(AuthorizationCompleted))]
        public async Task AuthorizationCompleted()
        {
            await starter.SignalEntityAsync(State.EntityId, nameof(AcmeContextActor.ValidateAuthorization), new ValidateAuthorizationInput { AuthorizationLocation = State.AuthorizationLocation, UseDns = true, OrchestratorId = State.OrchestratorId });


        }
    }
    public class ValidateAuthorizationInput
    {
        public bool UseDns { get; set; }
        public Uri AuthorizationLocation { get; set; }
        public string OrchestratorId { get;  set; }
    }
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

        //[Operation(nameof(UpdateDNS), Input = typeof(UpdateDNSInput))]
        //public void UpdateDNS(UpdateDNSInput updateDNSInput, ILogger log)
        //{  
        //    log.LogWarning("Please update Domain:\n\n domain={domain}\n dnsTxt={dnsTxt}\n location={location}\n updateUrl=http://localhost:7071/providers/DotNetDevOps.Letsencrypt/certificates/{instanceId}",updateDNSInput.Name, updateDNSInput.DnsTxt, updateDNSInput.Location, updateDNSInput.MonitorInstanceId);
            
        //}

        [Operation(nameof(ValidateAuthorization))]
        public async Task ValidateAuthorization(ValidateAuthorizationInput input)
        {

            var context = new AcmeContext(State.LetsEncryptEndpoint, KeyFactory.FromPem(State.Pem));
         
            var auth = context.Authorization(input.AuthorizationLocation);
            
            var authctx = input.UseDns ? await auth.Dns() : await auth.Http();
            await authctx.Validate();

            var status = await auth.Resource();

            var tcs = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            while (!tcs.IsCancellationRequested && status.Status == AuthorizationStatus.Pending)
            {
                await Task.Delay(5000);
                status = await auth.Resource();
            }

            await starter.RaiseEventAsync(input.OrchestratorId, "AuthorizationCompleted",authctx.Token);

        }

        [Operation(nameof(Initialize))]
        public async Task Initialize(AcmeContextInitializeInput input)
        { 
            if (State.SignerEmail != input.SignerEmail ||State.LetsEncryptEndpoint != input.LetsEncryptEndpoint)
            {
                State = new AcmeContextState();

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
            var context = new AcmeContext(State.LetsEncryptEndpoint, KeyFactory.FromPem(State.Pem));
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
                return new FinalizeOutput {  Pfx =pfx, Thumbprint = cert.Thumbprint, Name = "CN=" + input.Domains.First() };

            }

            throw new Exception(order?.Error?.ToString() ?? $"{orderCtx} is invalid");
        }

        [Operation(nameof(CreateOrder))]
        public async Task CreateOrder(OrderInput input, ILogger logger)
        {
            logger.LogInformation("Creating a new order for {domains}", input.Domains);

            input.UseDns01Authorization = input.UseDns01Authorization || input.Domains.Any(k => k.Contains("*"));

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
                        logger.LogWarning("Order is finished or broken");
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
                                    RequestMonitorInstanceId = input.MonitorInstanceId,
                                    UseDns01Authorization = input.UseDns01Authorization,
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
