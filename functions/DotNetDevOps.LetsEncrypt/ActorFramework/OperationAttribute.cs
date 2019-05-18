using System;



namespace DotNetDevOps.LetsEncrypt
{
    public class OperationAttribute : Attribute
    {
        public string Operation { get; private set; }
        public Type Input { get; set; }
        public OperationAttribute(string operation)
        {
            Operation = operation;
        }
    }
}
