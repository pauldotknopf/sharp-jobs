using System;

namespace SharpJobs
{
    public class JobFailedEvent
    {
        public JobFailedEvent(JobTask job, Exception ex)
        {
            Job = job;
            Exception = ex;
        }
        
        public JobTask Job { get; }
        
        public Exception Exception { get; }
    }
}