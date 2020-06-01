using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SharpJobs.Impl;
using Xunit;

namespace SharpJobs.Tests
{
    public class JobProcessorTests
    {
        private readonly IJobStorage _jobStorage;
        private readonly IJobProcessor _jobProcessor;

        public JobProcessorTests()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IJobStorage>(new MemoryJobStorage(new NullLogger<MemoryJobStorage>(), null));
            services.AddSingleton<IJobExecutor>(x => new JobExecutor(x.GetRequiredService<IServiceScopeFactory>(),
                new NullLogger<JobExecutor>(), x.GetRequiredService<IJobStorage>()));
            services.AddSingleton<IJobProcessor>(x => new SingleThreadJobProcessor(x.GetRequiredService<IJobStorage>(),
                x.GetRequiredService<IJobExecutor>(), new NullLogger<SingleThreadJobProcessor>()));
            services.AddTransient<TestJob>();
            
            var provider = services.BuildServiceProvider();

            _jobStorage = provider.GetRequiredService<IJobStorage>();
            _jobProcessor = provider.GetRequiredService<IJobProcessor>();
        }
        
        [Fact]
        public async Task Can_process_with_failed_jobs()
        {
            await _jobStorage.Enqueue<TestJob, TestJobData>(new TestJobData
            {
                Throw = true,
                Message = "testt"
            });
            var cancelToken = new CancellationTokenSource();
            JobFailedEvent failedEvent = null;
            _jobStorage.JobFailed.Subscribe(async (o, e) =>
            {
                failedEvent = e;
                cancelToken.Cancel();
                await Task.Yield();
            });
            _jobStorage.JobSucceeded.Subscribe((o, e) => throw new Exception("Shouldn't happen..."));
            
            cancelToken.CancelAfter(TimeSpan.FromSeconds(5)); // just in case
            await _jobProcessor.Process(cancelToken.Token);
            
            failedEvent.Should().NotBeNull();
            failedEvent.Exception.Message.Should().Be("testt");
        }
        
        [Fact]
        public async Task Can_process_with_successful_jobs()
        {
            await _jobStorage.Enqueue<TestJob, TestJobData>(new TestJobData
            {
                Throw = false
            });
            var cancelToken = new CancellationTokenSource();
            JobSucceededEvent succeededEvent = null;
            _jobStorage.JobSucceeded.Subscribe(async (o, e) =>
            {
                succeededEvent = e;
                cancelToken.Cancel();
                await Task.Yield();
            });
            _jobStorage.JobFailed.Subscribe((o, e) => throw new Exception("Shouldn't happen..."));
            
            cancelToken.CancelAfter(TimeSpan.FromSeconds(5)); // just in case
            await _jobProcessor.Process(cancelToken.Token);
            
            succeededEvent.Should().NotBeNull();
        }

        public class TestJob : AbstractJob<TestJobData>, IDisposable
        {
            public static int? Value { get; set; }
            public static bool? Disposed { get; set; }
            
            public override Task Run(TestJobData data)
            {
                if (data.Throw)
                {
                    throw new Exception(data.Message);
                }
                return Task.CompletedTask;
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }

        public class TestJobData
        {
            public bool Throw { get; set; }
            
            public string Message { get; set; }
        }
    }
}