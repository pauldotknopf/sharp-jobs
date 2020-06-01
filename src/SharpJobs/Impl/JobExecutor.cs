using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SharpJobs.Impl
{
    public class JobExecutor : IJobExecutor
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<JobExecutor> _logger;
        private readonly IJobStorage _jobStorage;

        public JobExecutor(IServiceScopeFactory serviceScopeFactory,
            ILogger<JobExecutor> logger,
            IJobStorage jobStorage)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            _jobStorage = jobStorage;
        }
        
        public async Task Execute(JobTask job)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                Exception jobException = null;
                try
                {
                    var instance = (IJob)scope.ServiceProvider.GetRequiredService(job.Type);
                    await instance.Run(job.Data);
                }
                catch (Exception ex)
                {
                    jobException = ex;
                    _logger.LogError(ex, "Couldn't run {@jobId} with {@type} with {@data}.", job.JobId, job.Type, job.Data);
                }

                try
                {
                    if (jobException == null)
                    {
                        await _jobStorage.MarkJobSucceeded(job.JobId);
                    }
                    else
                    {
                        await _jobStorage.MarkJobFailed(job.JobId, jobException);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("Error marking job as succeeded/failed", ex);
                }
            }
        }
    }
}