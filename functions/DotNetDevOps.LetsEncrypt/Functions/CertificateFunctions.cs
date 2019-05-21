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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using Microsoft.WindowsAzure.Storage.Blob;
using DotNetDevOps.LetsEncrypt.Functions.Models.Email;
using Microsoft.Extensions.Configuration;
using System.Net.Mail;
using System.Net.Mime;
using Newtonsoft.Json.Linq;
using DotNetDevOps.LetsEncrypt;
using Microsoft.Extensions.DependencyInjection;

[assembly: WebJobsStartup(typeof(StartUp))]

namespace DotNetDevOps.LetsEncrypt
{

    public class StartUp : IWebJobsStartup
    {
        public void Configure(IWebJobsBuilder builder)
        {
            builder.Services.AddTransient<EmailService>();
         //   var emailService = builder.Services.BuildServiceProvider().GetService<EmailService>();

//            emailService.SendEmailAsync("noreply@dotnetdevops.org", "DotNetDevOps Notifications", "info@kjeldager.com", "Certificate generated", "See attachment").Wait();

        }
    }
    public class EmailService
    {
        private readonly IConfiguration configuration;

        public EmailService(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public async Task SendEmailAsync(string from, string fromDisplay, string to, string subject, string html, params Attachment[] attachments)
        {
            MailMessage mailMsg = new MailMessage();
            foreach (var mail in to.ToLower().Split(',').Select(s => s.Trim()))
                mailMsg.To.Add(new MailAddress(mail));

            mailMsg.From = new MailAddress(from, fromDisplay);
            //  mailMsg.Bcc.Add(new MailAddress("pks@s-innovations.net"));

            mailMsg.Subject = subject;

            //  string html = message;
            mailMsg.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, null, MediaTypeNames.Text.Html));

            mailMsg.Headers.Add("X-SMTPAPI", JToken.FromObject(new { filters = new { opentrack = new { settings = new { enable = 0 } }, clicktrack = new { settings = new { enable = 0 } } } }).ToString());

            SmtpClient smtpClient = new SmtpClient("smtp.sendgrid.net", Convert.ToInt32(587));


            var sendgrid = configuration.GetValue<string>("dotnetdevops:sendgrid");

            var sendGridInfo = sendgrid.Split(':');
            // make below work!
            System.Net.NetworkCredential credentials = new System.Net.NetworkCredential(sendGridInfo.First(), string.Join(':', sendGridInfo.Skip(1)));
            foreach (var att in attachments) {
                mailMsg.Attachments.Add(att);
                    
                 }

            smtpClient.Credentials = credentials;
            await smtpClient.SendMailAsync(mailMsg);
        }
    }
    public class FinishRequestInput
    {
        public Target Target { get; set; }
        public FinalizeOutput Pfx { get; set; }
    }
    public class CertificateFunctions
    {
        private readonly EmailService emailService;

        public CertificateFunctions(EmailService emailService)
        {
            this.emailService = emailService;
        }


        [FunctionName(nameof(FinishRequest))]
        public async Task FinishRequest([ActivityTrigger] IDurableActivityContext ctx, ILogger logger)
        {
            var input = ctx.GetInput<FinishRequestInput>();
            var target = input.Target;
            var pfx = input.Pfx;

            if (target.Properties is FileSystemProperties filesystem)
            {
                File.WriteAllBytes(filesystem.Path, pfx.Pfx);
            }
            if (target.Properties is AzureBlobProperties azureBlob)
            {
                await new CloudBlockBlob(new Uri(azureBlob.TargetBlob)).UploadFromByteArrayAsync(pfx.Pfx, 0, pfx.Pfx.Length);
            }

            if (target.Properties is EmailTargetProperties email)
            {
                logger.LogWarning("Sending email with certificate to {email}",email.Email);
                // Create  the file attachment for this e-mail message.
                Attachment data = new Attachment(new MemoryStream(pfx.Pfx), pfx.Name + ".pfx", "application/x-pkcs12");
                // Add time stamp information for the file.


                await emailService.SendEmailAsync("noreply@dotnetdevops.org", "DotNetDevOps Notifications", email.Email, "Certificate generated","See attachment", data);
            }
        }

        [FunctionName(nameof(AddCertificateOrchestrator))]
        public async Task AddCertificateOrchestrator(
        [OrchestrationTrigger] IDurableOrchestrationContext ctx,
        [OrchestrationClient] IDurableOrchestrationClient starter,
        [ActorService(Name = "AcmeContext")] IActorProxy actorProxy,
        ILogger log)
        {
            var input = ctx.GetInput<AddCertificateRequest>();

            ctx.SetCustomStatus(new { status = "Pending" });




            var arcmeId = new EntityId("AcmeContext", input.SignerEmail.ToMD5Hash());


            {
                if (input.Target.Properties is AzureWebAppProperties azurewebapp)
                {
                    var site = await ctx.CallEntityAsync<SiteInner>(new EntityId("AzureWebsite", input.Target.Hash), nameof(AzureWebsiteActor.Load),
                        new LoadWebsiteInput
                        {
                            ResourceGroupname = azurewebapp.ResourceGroupName,
                            SiteName = azurewebapp.SiteName,
                            SlotName = azurewebapp.SlotName,
                            SubscriptionId = azurewebapp.SubscriptionId,
                            Domains = input.Domains
                        });
                   
                    ctx.SetCustomStatus(new { status = "SiteLoaded" });
                }
            }

            await ctx.CallEntityAsync(arcmeId, nameof(AcmeContextActor.Initialize),
            new AcmeContextInitializeInput { SignerEmail = input.SignerEmail, LetsEncryptEndpoint = input.LetsEncryptEndpoint });

            ctx.SetCustomStatus(new { status = "AcmeInitialized" });

            await ctx.CallEntityAsync(arcmeId, nameof(AcmeContextActor.CreateOrder),
                new OrderInput { Domains = input.Domains, MonitorInstanceId = ctx.InstanceId });

            await ctx.WaitForExternalEvent("Completed");

            ctx.SetCustomStatus(new { status = "OrderCreated" });

            var pfx = await ctx.CallEntityAsync<FinalizeOutput>(arcmeId, nameof(AcmeContextActor.FinalizeOrder),
                new FinalizeInput { CsrInfo = input.CsrInfo, Domains = input.Domains });

            ctx.SetCustomStatus(new { status = "OrderFinalized" });

            await ctx.CallActivityAsync(nameof(FinishRequest), new FinishRequestInput { Target = input.Target, Pfx = pfx });

            {
                if (input.Target.Properties is AzureWebAppProperties azurewebapp)
                {
                    await ctx.CallEntityAsync<SiteInner>(new EntityId("AzureWebsite", input.Target.Hash),
                        nameof(AzureWebsiteActor.UpdateCertificate), new UpdateCertificateInput { Pfx = pfx, Domains = input.Domains, UseIpBasedSsl = azurewebapp.UseIpBasedSsl });

                }
            }

            ctx.SetCustomStatus(new { status = "CertificateUpdated" });



        }

        [FunctionName("CreateCertificateRequest")]
        public static async Task<HttpResponseMessage> Run(
         [HttpTrigger(AuthorizationLevel.Function, "post", Route = "providers/DotNetDevOps.LetsEncrypt/certificates")] HttpRequestMessage req,
         [OrchestrationClient] IDurableOrchestrationClient starter,
         ILogger log)
        {
            var addCertificateRequest = await req.Content.ReadAsAsync<AddCertificateRequest>();

            var instanceId = await starter.StartNewAsync(nameof(AddCertificateOrchestrator), addCertificateRequest);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return await starter.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, TimeSpan.FromSeconds(3));
        }

        [FunctionName("ChallengeCompleted")]
        public static async Task userUpdated(
       [HttpTrigger(AuthorizationLevel.Function, "get", Route = "providers/DotNetDevOps.LetsEncrypt/challenges/{token}")] HttpRequestMessage req,
       [OrchestrationClient] IDurableOrchestrationClient starter, string token,
       ILogger log)
        {
            await starter.SignalEntityAsync(new EntityId("Authorization", token), nameof(AuthorizationActor.AuthorizationCompleted));

        }

        [FunctionName("AcmeChallenge")]
        public static async Task<IActionResult> AcmeChallenge(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = ".well-known/acme-challenge/{token}")] HttpRequest req,
        [OrchestrationClient] IDurableOrchestrationClient starter, string token,
        ILogger log)
        {

            log.LogInformation("Acme-Challenge request for {Host}", req.Host.Host);

            var state = await starter.ReadEntityStateAsync<AuthorizationActorState>(new EntityId("Authorization", token));

            return new ContentResult() { Content = state.EntityState.KeyAuthz, ContentType = "plain/text", StatusCode = 200 };
            //   await ctx.Response.WriteAsync(keyAuthString);
        }

    }
}
