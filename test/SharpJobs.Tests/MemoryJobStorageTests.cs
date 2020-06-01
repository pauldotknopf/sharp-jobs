using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using SharpJobs.Impl;
using Xunit;

namespace SharpJobs.Tests
{
    public class MemoryJobStorageTests
    {
        private readonly IJobStorage _jobStorage;
        
        public MemoryJobStorageTests()
        {
            _jobStorage = new MemoryJobStorage(new NullLogger<MemoryJobStorage>(), null);
        }
        
        [Fact]
        public async Task Can_enqueue_and_deque_jobs()
        {
            var enqueuedEvents = new List<JobEnqueuedEvent>();
            _jobStorage.JobEnqueued.Subscribe((o, e) =>
            {
                enqueuedEvents.Add(e);
                return Task.CompletedTask;
            });
            await _jobStorage.Enqueue<TestJob, TestJobData>(new TestJobData
            {
                Value = 2
            });
            enqueuedEvents.Count.Should().Be(1);
            ((TestJobData) enqueuedEvents[0].Job.Data).Value.Should().Be(2);
            await _jobStorage.Enqueue<TestJob, TestJobData>(new TestJobData
            {
                Value = 3
            });
            enqueuedEvents.Count.Should().Be(2);
            ((TestJobData) enqueuedEvents[1].Job.Data).Value.Should().Be(3);
            
            var job1 = await _jobStorage.DequeueJob();
            job1.Should().NotBeNull();
            job1.Data.Should().BeOfType<TestJobData>();
            ((TestJobData) job1.Data).Value.Should().Be(2);
            var job2 = await _jobStorage.DequeueJob();
            job2.Should().NotBeNull();
            job2.Data.Should().BeOfType<TestJobData>();
            ((TestJobData) job2.Data).Value.Should().Be(3);
            var job3 = await _jobStorage.DequeueJob();
            job3.Should().BeNull();
        }

        [Fact]
        public async Task Can_mark_job_as_succeeded_and_failed()
        {
            var succeededEvents = new List<JobSucceededEvent>();
            var failedEvents = new List<JobFailedEvent>();
            _jobStorage.JobSucceeded.Subscribe((o, e) =>
            {
                succeededEvents.Add(e);
                return Task.CompletedTask;
            });
            _jobStorage.JobFailed.Subscribe((o, e) =>
            {
                failedEvents.Add(e);
                return Task.CompletedTask;
            });
            
            await _jobStorage.Enqueue<TestJob, TestJobData>(new TestJobData {Value = 33});
            await _jobStorage.Enqueue<TestJob, TestJobData>(new TestJobData {Value = 44});
            var job = await _jobStorage.DequeueJob();
            await _jobStorage.MarkJobSucceeded(job.JobId);

            failedEvents.Count.Should().Be(0);
            succeededEvents.Count.Should().Be(1);
            ((TestJobData) succeededEvents[0].Job.Data).Value.Should().Be(33);

            job = await _jobStorage.DequeueJob();
            await _jobStorage.MarkJobFailed(job.JobId, new Exception("oops"));
            succeededEvents.Count.Should().Be(1);
            failedEvents.Count.Should().Be(1);
            ((TestJobData) failedEvents[0].Job.Data).Value.Should().Be(44);
            failedEvents[0].Exception.Message.Should().Be("oops");
        }

        [Fact]
        public async Task Can_get_all_jobs()
        {
            await _jobStorage.Enqueue<TestJob, TestJobData>(new TestJobData
            {
                Value = 1
            });
            await _jobStorage.Enqueue<TestJob, TestJobData>(new TestJobData
            {
                Value = 2
            });

            var jobs = await _jobStorage.GetAllJobs();

            jobs.Count.Should().Be(2);
            jobs[0].Data.As<TestJobData>().Value.Should().Be(1);
            jobs[1].Data.As<TestJobData>().Value.Should().Be(2);
        }

        [Fact]
        public async Task Can_get_jobs_of_type()
        {
            await _jobStorage.Enqueue<TestJob, TestJobData>(new TestJobData
            {
                Value = 1
            });
            await _jobStorage.Enqueue<TestJob2, TestJobData>(new TestJobData
            {
                Value = 2
            });

            var jobs = await _jobStorage.GetAllJobs<TestJob>();

            jobs.Count.Should().Be(1);
            jobs[0].Data.As<TestJobData>().Value.Should().Be(1);
            
            jobs = await _jobStorage.GetAllJobs<TestJob2>();

            jobs.Count.Should().Be(1);
            jobs[0].Data.As<TestJobData>().Value.Should().Be(2);
        }

        [Fact]
        public async Task Can_re_queue_processing_jobs()
        {
            await _jobStorage.Enqueue<TestJob, TestJobData>(new TestJobData
            {
                Value = 1
            });
            
            // Make the job as processing
            var job1 = await _jobStorage.DequeueJob();

            await _jobStorage.ReQueueProcessingJobs();

            var job2 = await _jobStorage.DequeueJob();

            job1.JobId.Should().Be(job2.JobId);
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public class TestJob : AbstractJob<TestJobData>
        {
            public override Task Run(TestJobData data)
            {
                return Task.CompletedTask;
            }
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public class TestJob2 : AbstractJob<TestJobData>
        {
            public override Task Run(TestJobData data)
            {
                return Task.CompletedTask;
            }
        }

        public class TestJobData
        {
            public int Value { get; set; }
        }
    }
}