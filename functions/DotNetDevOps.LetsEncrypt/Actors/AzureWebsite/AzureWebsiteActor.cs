using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Linq;
using System;
using Microsoft.Azure.Management.AppService.Fluent.Models;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Rest;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Services.AppAuthentication;

namespace DotNetDevOps.LetsEncrypt
{
    [ActorService(Name = "AzureWebsite")]
    public class AzureWebsiteActor : Actor<AzureWebsiteActorState>
    {
        private static async Task<WebSiteManagementClient> CreateWebSiteManagementClientAsync(string subscriptionId)
        {
            var tokenProvider = new AzureServiceTokenProvider();

            var accessToken = await tokenProvider.GetAccessTokenAsync("https://management.azure.com/");
            var tokenCredentials = new TokenCredentials(accessToken);
            var azureCredentials = new AzureCredentials(
           tokenCredentials,
           tokenCredentials,
           "common",
           AzureEnvironment.AzureGlobalCloud);

            var client = RestClient
            .Configure()
            .WithEnvironment(AzureEnvironment.AzureGlobalCloud)
            .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
            .WithCredentials(azureCredentials)
            .Build();

            var websiteClient = new WebSiteManagementClient(client)
            {
                SubscriptionId = subscriptionId
            };

            return websiteClient;
        }


        [Operation(nameof(Load), Input = typeof(LoadWebsiteInput))]
        public async Task<SiteInner> Load(LoadWebsiteInput input, ILogger log)
        {
            State.SubscriptionId = input.SubscriptionId;
            var websiteClient = await CreateWebSiteManagementClientAsync(input.SubscriptionId);

            if (!string.IsNullOrEmpty(input.SlotName))
            {
                State.Site = await websiteClient.WebApps.GetSlotAsync(input.ResourceGroupname, input.SiteName, input.SlotName);
            }
            else
            {

                State.Site = await websiteClient.WebApps.GetAsync(input.ResourceGroupname, input.SiteName);
            }

            var hostNameSslStates = State.Site.HostNameSslStates
                                       .Where(x => input.Domains.Contains(x.Name))
                                       .ToArray();

            if (hostNameSslStates.Length != input.Domains.Length)
            {
                foreach (var hostName in input.Domains.Except(hostNameSslStates.Select(x => x.Name)))
                {
                    log.LogError($"{hostName} is not found");
                }
                return null;
            }

            return State.Site;

        }

        [Operation(nameof(UpdateCertificate), Input = typeof(LoadWebsiteInput))]
        public async Task UpdateCertificate(UpdateCertificateInput input, ILogger log)
        {
         


            var websiteClient = await CreateWebSiteManagementClientAsync(State.SubscriptionId);

            await websiteClient.Certificates.CreateOrUpdateAsync(State.Site.ResourceGroup, $"{input.Domains[0]}-{input.Pfx.Thumbprint}", new CertificateInner
            {
                Location = State.Site.Location,
                Password = "",
                PfxBlob = input.Pfx.Pfx,
                ServerFarmId = State.Site.ServerFarmId
            });

            var hostNameSslStates = State.Site.HostNameSslStates
                                  .Where(x => input.Domains.Contains(x.Name))
                                  .ToArray();



            foreach (var hostNameSslState in hostNameSslStates)
            {
                hostNameSslState.Thumbprint = input.Pfx.Thumbprint;
                hostNameSslState.SslState = input.UseIpBasedSsl ?? false ? SslState.IpBasedEnabled : SslState.SniEnabled;
                hostNameSslState.ToUpdate = true;
            }

            await websiteClient.WebApps.CreateOrUpdateAsync(State.Site.ResourceGroup,State.Site.Name, State.Site);

        }
    }
}
