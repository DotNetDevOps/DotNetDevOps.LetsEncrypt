using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNetDevOps.LetsEncrypt.Functions.Models.Email
{
    public class EmailTargetProperties :TargetProperties
    {
       public string Email { get; set; }
      
    }
}
