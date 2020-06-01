namespace SharpJobs
{
    public class JobEnqueuedEvent
    {
        public JobEnqueuedEvent(JobTask job)
        {
            Job = job;
        }
        
        public JobTask Job { get; }
    }
}