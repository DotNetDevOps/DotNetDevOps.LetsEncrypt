namespace DotNetDevOps.LetsEncrypt
{
    public class FinalizeOutput
    {
        public byte[] Pfx { get; set; }
        public string Thumbprint { get; set; }
        public string Name { get;  set; }
    }
}
