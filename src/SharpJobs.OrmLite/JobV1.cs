using System;
using ServiceStack.DataAnnotations;

namespace SharpJobs.OrmLite
{
    [Alias("jobs")]
    public class JobV1
    {
        public JobV1()
        {
            QueuedOn = DateTimeOffset.UtcNow;
            Status = JobStatus.Queued;
        }
        
        [Alias("id"), AutoIncrement, PrimaryKey, Required]
        public int Id { get; set; }
        
        [Alias("queued_on"), Required]
        public DateTimeOffset QueuedOn { get; set; }
        
        [Alias("status"), Required]
        public JobStatus Status { get; set; }
        
        [Alias("job_type"), Required]
        public string JobType { get; set; }
        
        [Alias("job_data_type"), Required]
        public string JobDataType { get; set; }
        
        [Alias("job_data"), Required]
        public string JobData { get; set; }
    }
}