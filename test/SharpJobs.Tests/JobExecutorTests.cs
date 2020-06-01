using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SharpJobs.Impl;
using Xunit;

namespace SharpJobs.Tests
{
    public class JobExecutorTests
    {
        private readonly IJobStorage _jobStorage;
        private readonly IJobExecutor _jobExecutor;
        
        public JobExecutorTests()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IJobStorage>(new MemoryJobStorage(new NullLogger<MemoryJobStorage>(), null));
            services.AddSingleton<IJobExecutor>(x => new JobExecutor(x.GetRequiredService<IServiceScopeFactory>(),
                new NullLogger<JobExecutor>(), x.GetRequiredService<IJobStorage>()));
            services.AddTransient<TestJob>();

            var provider = services.BuildServiceProvider();

            _jobStorage = provider.GetRequiredService<IJobStorage>();
            _jobExecutor = provider.GetRequiredService<IJobExecutor>();
        }

        [Fact]
        public async Task Can_run_job()
        {
            await _jobStorage.Enqueue<TestJob, TestJobData>(new TestJobData
            {
                Value = 23
            });
            
            await _jobExecutor.Execute(await _jobStorage.DequeueJob());
            TestJob.Value.Should().Be(23);
            TestJob.Disposed.Should().Be(true);
        }

        public class TestJob : AbstractJob<TestJobData>, IDisposable
        {
            public static int? Value { get; set; }
            public static bool? Disposed { get; set; }
            
            public override Task Run(TestJobData data)
            {
                Value = data.Value;
                return Task.CompletedTask;
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }

        public class TestJobData
        {
            public int Value { get; set; }
        }
    }
}