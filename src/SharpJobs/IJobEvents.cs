using System.Threading.Tasks;
using SharpEvents;

namespace SharpJobs
{
    public interface IJobEvents
    {
        IAsyncEventAggregatorConsumer<JobEnqueuedEvent> JobEnqueued { get; }

        IAsyncEventAggregatorConsumer<JobSucceededEvent> JobSucceeded { get; }
        
        IAsyncEventAggregatorConsumer<JobFailedEvent> JobFailed { get; }
    }
}