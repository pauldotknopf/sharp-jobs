using System.Threading.Tasks;

namespace SharpJobs
{
    public interface IJob
    {
        Task Run(object data);
    }
    
    public abstract class AbstractJob<TData> : IJob where TData: new()
    {
        Task IJob.Run(object data)
        {
            return Run((TData)data);
        }

        public abstract Task Run(TData data);
    }
}