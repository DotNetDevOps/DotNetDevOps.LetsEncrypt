using Microsoft.Azure.WebJobs;




namespace DotNetDevOps.LetsEncrypt
{
    public class Actor<T>
    {
        public EntityId Id { get; internal set; }
        public IDurableEntityContext Context { get; internal set; }
        public T State { get; internal set; }

        public void SaveState()
        {
            Context.SetState(this.State);

        }
    }
}
