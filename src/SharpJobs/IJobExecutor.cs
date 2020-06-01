using System.Threading.Tasks;

namespace SharpJobs
{
    public interface IJobExecutor
    {
        Task Execute(JobTask job);
    }
}