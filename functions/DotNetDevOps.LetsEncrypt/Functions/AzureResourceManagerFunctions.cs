using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Management.AppService.Fluent.Models;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Rest;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.WebJobs.Hosting;



namespace DotNetDevOps.LetsEncrypt
{
    //public static class AzureResourceManagerFunctions
    //{
    //    private static async Task<WebSiteManagementClient> CreateWebSiteManagementClientAsync(string subscriptionId)
    //    {
    //        var tokenProvider = new AzureServiceTokenProvider();

    //        var accessToken = await tokenProvider.GetAccessTokenAsync("https://management.azure.com/");
    //        var tokenCredentials = new TokenCredentials(accessToken);
    //        var azureCredentials = new AzureCredentials(
    //       tokenCredentials,
    //       tokenCredentials,
    //       "common",
    //       AzureEnvironment.AzureGlobalCloud);

    //        var client = RestClient
    //        .Configure()
    //        .WithEnvironment(AzureEnvironment.AzureGlobalCloud)
    //        .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
    //        .WithCredentials(azureCredentials)
    //        .Build();

    //        var websiteClient = new WebSiteManagementClient(client)
    //        {
    //            SubscriptionId = subscriptionId
    //        };

    //        return websiteClient;
    //    }



    //    [FunctionName(nameof(GetSite))]
    //    public static async Task<SiteInner> GetSite([ActivityTrigger] IDurableActivityContext context, ILogger log)
    //    {


    //        var (subscriptionId, resourceGroupName, siteName, slotName) = context.GetInput<(string, string, string, string)>();
    //        var websiteClient = await CreateWebSiteManagementClientAsync(subscriptionId);

    //        if (!string.IsNullOrEmpty(slotName))
    //        {
    //            return await websiteClient.WebApps.GetSlotAsync(resourceGroupName, siteName, slotName);
    //        }

    //        return await websiteClient.WebApps.GetAsync(resourceGroupName, siteName);
    //    }
    //}
}
