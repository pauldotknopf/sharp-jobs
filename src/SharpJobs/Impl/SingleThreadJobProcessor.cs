using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SharpJobs.Impl
{
    public class SingleThreadJobProcessor : IJobProcessor
    {
        private readonly IJobStorage _jobStorage;
        private readonly IJobExecutor _jobExecutor;
        private readonly ILogger<SingleThreadJobProcessor> _logger;

        public SingleThreadJobProcessor(IJobStorage jobStorage,
            IJobExecutor jobExecutor,
            ILogger<SingleThreadJobProcessor> logger)
        {
            _jobStorage = jobStorage;
            _jobExecutor = jobExecutor;
            _logger = logger;
        }
        
        public async Task Process(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                JobTask job;
                
                try
                {
                    job = await _jobStorage.DequeueJob();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Couldn't dequeue a job to run.");
                    await Task.Run(() => { token.WaitHandle.WaitOne(TimeSpan.FromSeconds(5)); }, token);
                    
                    continue;
                }

                if (job == null)
                {
                    await Task.Run(() => { token.WaitHandle.WaitOne(TimeSpan.FromSeconds(5)); }, token);
                }
                else
                {
                    try
                    {
                        await _jobExecutor.Execute(job);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Problem executing job.");
                    }
                }
            }
        }

        public Task StartLongRunningTask(CancellationToken token)
        {
            return Process(token);
        }
    }
}