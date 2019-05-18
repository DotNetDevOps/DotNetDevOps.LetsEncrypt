using System;
using Microsoft.Azure.WebJobs.Description;



namespace DotNetDevOps.LetsEncrypt
{
    [Binding]
    public class ActorServiceAttribute:Attribute
    {
        public string Name { get; set; }
    }
}
