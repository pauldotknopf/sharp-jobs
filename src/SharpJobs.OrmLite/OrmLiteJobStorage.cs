using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ServiceStack.OrmLite;
using SharpDataAccess.Data;
using SharpEvents;

namespace SharpJobs.OrmLite
{
    public class OrmLiteJobStorage : IJobStorage
    {
        private readonly IDataService _dataService;
        private readonly ILogger<OrmLiteJobStorage> _logger;
        private readonly AsyncEventAggregator<JobEnqueuedEvent> _jobEnqueued;
        private readonly AsyncEventAggregator<JobSucceededEvent> _jobSucceeded;
        private readonly AsyncEventAggregator<JobFailedEvent> _jobFailed;

        public OrmLiteJobStorage(IDataService dataService,
            ILogger<OrmLiteJobStorage> logger,
            SharpEventDispatcherAsyncDel eventDispatcher)
        {
            _dataService = dataService;
            _logger = logger;
            _jobEnqueued = new AsyncEventAggregator<JobEnqueuedEvent>(eventDispatcher);
            _jobSucceeded = new AsyncEventAggregator<JobSucceededEvent>(eventDispatcher);
            _jobFailed = new AsyncEventAggregator<JobFailedEvent>(eventDispatcher);
        }
        
        public async Task Enqueue<T, TData>(TData data) where T : IJob where TData : new()
        {
            var job = new JobV1
            {
                QueuedOn = DateTimeOffset.UtcNow,
                JobType = typeof(T).AssemblyQualifiedName,
                JobDataType = typeof(TData).AssemblyQualifiedName,
                JobData = JsonConvert.SerializeObject(data)
            };

            using (var connection = new ConScope(_dataService))
            {
                using (var transaction = await connection.BeginTransaction())
                {
                    await connection.Connection.SaveAsync(job);
                    
                    transaction.Commit();
                }
            }
            
            await _jobEnqueued.Publish(this, new JobEnqueuedEvent(new JobTask
            {
                JobId = job.Id,
                Type = typeof(T),
                Data = data
            }));
        }

        public async Task<JobTask> DequeueJob()
        {
            JobV1 result;

            using (var connection = new ConScope(_dataService))
            {
                using (var transaction = await connection.BeginTransaction())
                {
                    result = await connection.Connection.SingleAsync(
                        connection.Connection.From<JobV1>()
                            .Where(x => x.Status == JobStatus.Queued)
                            .OrderBy(x => x.QueuedOn)
                            .Take(1));

                    if (result == null)
                    {
                        return null;
                    }

                    result.Status = JobStatus.Processing;

                    await connection.Connection.UpdateAsync(result);
                    
                    transaction.Commit();
                }
            }
            
            return new JobTask
            {
                JobId = result.Id,
                Type = Type.GetType(result.JobType),
                Data = JsonConvert.DeserializeObject(result.JobData, Type.GetType(result.JobDataType), (JsonSerializerSettings)null)
            };
        }

        public async Task MarkJobSucceeded(int jobId)
        {
            JobV1 job;

            using (var connection = new ConScope(_dataService))
            {
                using (var transaction = await connection.BeginTransaction())
                {
                    job = await connection.Connection.SingleByIdAsync<JobV1>(jobId);
                    
                    if (job == null)
                    {
                        throw new Exception($"Invalid job id {jobId}");
                    }

                    if (job.Status != JobStatus.Processing)
                    {
                        throw new Exception($"Can't mark a job {jobId} as completed because it wasn't marked as processing.");
                    }

                    await connection.Connection.DeleteAsync(job);
                    
                    transaction.Commit();
                }
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
            JobV1 job;

            using (var connection = new ConScope(_dataService))
            {
                using (var transaction = await connection.BeginTransaction())
                {
                    job = await connection.Connection.SingleByIdAsync<JobV1>(jobId);
                    
                    if (job == null)
                    {
                        throw new Exception($"Invalid job id {jobId}");
                    }

                    if (job.Status != JobStatus.Processing)
                    {
                        throw new Exception($"Can't mark a job {jobId} as failed because it wasn't marked as processing.");
                    }

                    await connection.Connection.DeleteAsync(job);
                    
                    transaction.Commit();
                }
            }
            
            await _jobFailed.Publish(this, new JobFailedEvent(new JobTask
            {
                JobId = job.Id,
                Type = Type.GetType(job.JobType),
                Data = JsonConvert.DeserializeObject(job.JobData, Type.GetType(job.JobDataType))
            }, ex));
        }

        public async Task<List<JobTask>> GetAllJobs()
        {
            using (var connection = new ConScope(_dataService))
            {
                var jobs = await connection.Connection.SelectAsync(connection.Connection.From<JobV1>()
                    .OrderBy(x => x.QueuedOn));

                return jobs.Select(x => new JobTask
                {
                    JobId = x.Id,
                    Type = Type.GetType(x.JobType),
                    Data = JsonConvert.DeserializeObject(x.JobData, Type.GetType(x.JobDataType), (JsonSerializerSettings) null)
                }).ToList();
            }
        }

        public async Task<List<JobTask>> GetAllJobs<T>() where T : IJob
        {
            using (var connection = new ConScope(_dataService))
            {
                var jobType = typeof(T).AssemblyQualifiedName;
                var jobs = await connection.Connection.SelectAsync(connection.Connection.From<JobV1>()
                    .Where(x => x.JobType == jobType)
                    .OrderBy(x => x.QueuedOn));

                return jobs.Select(x => new JobTask
                {
                    JobId = x.Id,
                    Type = Type.GetType(x.JobType),
                    Data = JsonConvert.DeserializeObject(x.JobData, Type.GetType(x.JobDataType), (JsonSerializerSettings) null)
                }).ToList();
            }
        }

        public async Task ReQueueProcessingJobs()
        {
            using (var connection = new ConScope(_dataService))
            {
                using (var transaction = await connection.BeginTransaction())
                {
                    connection.Connection.UpdateOnly(() => new JobV1 {Status = JobStatus.Queued},
                        x => x.Status == JobStatus.Processing);
                    transaction.Commit();
                }
            }
        }
        
        public async Task RemoveProblematicJobs()
        {
            using (var connection = new ConScope(_dataService))
            {
                using (var transaction = await connection.BeginTransaction())
                {
                    var jobs = await connection.Connection.SelectAsync(connection.Connection.From<JobV1>());
                    foreach (var job in jobs)
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
                            await connection.Connection.DeleteByIdAsync<JobV1>(job.Id);
                        }
                    }
                    
                    transaction.Commit();
                }
            }
        }
        
        public IAsyncEventAggregatorConsumer<JobEnqueuedEvent> JobEnqueued => _jobEnqueued;

        public IAsyncEventAggregatorConsumer<JobSucceededEvent> JobSucceeded => _jobSucceeded;

        public IAsyncEventAggregatorConsumer<JobFailedEvent> JobFailed => _jobFailed;
    }
}