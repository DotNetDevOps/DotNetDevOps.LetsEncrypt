namespace DotNetDevOps.LetsEncrypt
{
    public class UpdateCertificateInput
    {
        public FinalizeOutput Pfx { get; set; }
        public string[] Domains { get; set; }
        public bool? UseIpBasedSsl { get; set; }

    }
}
