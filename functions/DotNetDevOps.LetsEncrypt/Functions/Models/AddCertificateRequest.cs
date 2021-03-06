﻿using System;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using Certes.Acme;
using Microsoft.Azure.WebJobs.Hosting;
using Certes;
using System.Collections.Generic;

namespace DotNetDevOps.LetsEncrypt
{
    public class AddCertificateRequest
    {
        [Required] 
        public string SignerEmail { get; set; }
       
        [Required]
        public string[] Domains { get; set; }


        public Uri LetsEncryptEndpoint { get; set; } = WellKnownServers.LetsEncryptV2;
        public CsrInfo CsrInfo { get; set; }

       
        public Target Target { get; set; }

        public bool UseDns01Authorization { get; set; }
    }
}
