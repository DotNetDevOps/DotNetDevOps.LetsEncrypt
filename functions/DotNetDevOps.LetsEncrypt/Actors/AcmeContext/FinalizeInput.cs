﻿using Certes;
using System;

namespace DotNetDevOps.LetsEncrypt
{
    public class FinalizeInput
    {
        public CsrInfo CsrInfo { get; set; }
         
        public string[] Domains { get;  set; }
    }
}
