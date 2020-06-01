namespace SharpJobs
{
    public class JobSucceededEvent
    {
        public JobSucceededEvent(JobTask job)
        {
            Job = job;
        }
        
        public JobTask Job { get; }
    }
}