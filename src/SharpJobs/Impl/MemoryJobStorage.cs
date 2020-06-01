using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SharpEvents;

namespace SharpJobs.Impl
{
    public class MemoryJobStorage : IJobStorage
    {
        private readonly ILogger<MemoryJobStorage> _logger;
        private readonly List<Job> _jobs = new List<Job>();
        private readonly object _lock = new object();
        private readonly AsyncEventAggregator<JobEnqueuedEvent> _jobEnqueued;
        private readonly AsyncEventAggregator<JobSucceededEvent> _jobSucceeded;
        private readonly AsyncEventAggregator<JobFailedEvent> _jobFailed;
        
        public MemoryJobStorage(ILogger<MemoryJobStorage> logger, SharpEventDispatcherAsyncDel eventDispatcher)
        {
            _logger = logger;
            _jobEnqueued = new AsyncEventAggregator<JobEnqueuedEvent>(eventDispatcher);
            _jobSucceeded = new AsyncEventAggregator<JobSucceededEvent>(eventDispatcher);
            _jobFailed = new AsyncEventAggregator<JobFailedEvent>(eventDispatcher);
        }
        
        public async Task Enqueue<T, TData>(TData data) where T : IJob where TData : new()
        {
            var job = new Job
            {
                QueuedOn = DateTimeOffset.UtcNow,
                JobType = typeof(T).AssemblyQualifiedName,
                JobDataType = typeof(TData).AssemblyQualifiedName,
                JobData = JsonConvert.SerializeObject(data)
            };

            lock(_lock)
            {
                _jobs.Add(job);
            }

            await _jobEnqueued.Publish(this, new JobEnqueuedEvent(new JobTask
            {
                JobId = job.Id,
                Type = typeof(T),
                Data = data
            }));
        }

        public Task<JobTask> DequeueJob()
        {
            Job result;

            lock (_lock)
            {
                result = _jobs.Where(x => x.Status == JobStatus.Queued).OrderBy(x => x.QueuedOn).FirstOrDefault();
            }

            if (result == null)
            {
                return Task.FromResult<JobTask>(null);
            }

            result.Status = JobStatus.Processing;

            return Task.FromResult(new JobTask
            {
                JobId = result.Id,
                Type = Type.GetType(result.JobType),
                Data = JsonConvert.DeserializeObject(result.JobData, Type.GetType(result.JobDataType),
                    (JsonSerializerSettings) null)
            });
        }

        public async Task MarkJobSucceeded(int jobId)
        {
            Job job;
            
            lock (_lock)
            {
                job = _jobs.FirstOrDefault(x => x.Id == jobId);
            
                if (job == null)
                {
                    throw new Exception($"Invalid job id {jobId}");
                }

                if (job.Status != JobStatus.Processing)
                {
                    throw new Exception($"Can't mark a job {jobId} as completed because it wasn't marked as processing.");
                }

                _jobs.Remove(job);
            }
            
            await _jobSucceeded.Publish(this, new JobSucceededEvent(new JobTask
            {
                JobId = job.Id,
                Type = Type.GetType(job.JobType),
                Data = JsonConvert.DeserializeObject(job.JobData, Type.GetType(job.JobDataType))
            }));
        }

        public async Task MarkJobFailed(int jobId, Exception ex)
        {
            Job job;
            
            lock (_lock)
            {
                job = _jobs.FirstOrDefault(x => x.Id == jobId);

                if (job == null)
                {
                    throw new Exception($"Invalid job id {jobId}");
                }

                if (job.Status != JobStatus.Processing)
                {
                    throw new Exception($"Can't mark a job {jobId} as failed because it wasn't marked as processing.");
                }

                _jobs.Remove(job);
            }
            
            await _jobFailed.Publish(this, new JobFailedEvent(new JobTask
            {
                JobId = job.Id,
                Type = Type.GetType(job.JobType),
                Data = JsonConvert.DeserializeObject(job.JobData, Type.GetType(job.JobDataType))
            }, ex));
        }

        public Task<List<JobTask>> GetAllJobs()
        {
            lock (_lock)
            {
                return Task.FromResult(_jobs.OrderBy(x => x.QueuedOn).Select(x => new JobTask
                {
                    JobId = x.Id,
                    Type = Type.GetType(x.JobType),
                    Data = JsonConvert.DeserializeObject(x.JobData, Type.GetType(x.JobDataType),
                        (JsonSerializerSettings) null)
                }).ToList());
            }
        }

        public Task<List<JobTask>> GetAllJobs<T>() where T : IJob
        {
            var jobType = typeof(T).AssemblyQualifiedName;
            
            lock (_lock)
            {
                var jobs = _jobs.Where(x => x.JobType == jobType).OrderBy(x => x.QueuedOn);

                return Task.FromResult( jobs.Select(x => new JobTask
                {
                    JobId = x.Id,
                    Type = Type.GetType(x.JobType),
                    Data = JsonConvert.DeserializeObject(x.JobData, Type.GetType(x.JobDataType),
                        (JsonSerializerSettings) null)
                }).ToList());
            }
        }

        public Task ReQueueProcessingJobs()
        {
            lock (_lock)
            {
                foreach (var job in _jobs)
                {
                    if (job.Status == JobStatus.Processing)
                        job.Status = JobStatus.Queued;
                }

                return Task.CompletedTask;
            }
        }
        
        public Task RemoveProblematicJobs()
        {
            lock (_lock)
            {
                foreach (var job in _jobs)
                {
                    try
                    {
                        var jobType = Type.GetType(job.JobType);
                        if (jobType == null)
                        {
                            throw new Exception($"Invalid job type: {job.JobType}");
                        }

                        var jobDataType = Type.GetType(job.JobDataType);
                        if (jobDataType == null)
                        {
                            throw new Exception($"Invalid job type: {job.JobDataType}");
                        }

                        JsonConvert.DeserializeObject(job.JobData, Type.GetType(job.JobDataType),
                            (JsonSerializerSettings) null);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Detected problematic job, deleting");
                        _jobs.Remove(job);
                    }
                }
            }

            return Task.CompletedTask;
        }

        public IAsyncEventAggregatorConsumer<JobEnqueuedEvent> JobEnqueued => _jobEnqueued;

        public IAsyncEventAggregatorConsumer<JobSucceededEvent> JobSucceeded => _jobSucceeded;

        public IAsyncEventAggregatorConsumer<JobFailedEvent> JobFailed => _jobFailed;
        
        class Job
        {
            public Job()
            {
                QueuedOn = DateTimeOffset.UtcNow;
                Status = JobStatus.Queued;
            }
        
            public int Id { get; set; }
        
            public DateTimeOffset QueuedOn { get; set; }
        
            public JobStatus Status { get; set; }
        
            public string JobType { get; set; }
        
            public string JobDataType { get; set; }
        
            public string JobData { get; set; }
        }
        
        enum JobStatus
        {
            Queued,
            Processing,
            Succeeded,
            Failed
        }
    }
}