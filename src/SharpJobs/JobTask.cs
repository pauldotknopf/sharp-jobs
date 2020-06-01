using System;

namespace SharpJobs
{
    public class JobTask
    {
        public int JobId { get; set; }
        
        public Type Type { get; set; }
        
        public object Data { get; set; }
    }
}