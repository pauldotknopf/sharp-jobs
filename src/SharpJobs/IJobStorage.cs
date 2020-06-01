using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharpJobs
{
    public interface IJobStorage : IJobEvents
    {
        Task Enqueue<T, TData>(TData data) where T : IJob where TData : new();

        Task<JobTask> DequeueJob();

        Task MarkJobSucceeded(int jobId);

        Task MarkJobFailed(int jobId, Exception ex);

        Task<List<JobTask>> GetAllJobs();

        Task<List<JobTask>> GetAllJobs<T>() where T : IJob;

        Task ReQueueProcessingJobs();

        Task RemoveProblematicJobs();
    }
}