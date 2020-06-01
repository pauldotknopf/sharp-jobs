using System.Threading;
using System.Threading.Tasks;

namespace SharpJobs
{
    public interface IJobProcessor
    {
        Task Process(CancellationToken token);
    }
}