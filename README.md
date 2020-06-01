# SharpJobs

A very simple low-friction approach to procesing jobs.

[![SharpJobs](https://img.shields.io/nuget/v/SharpJobs.svg?style=flat-square&label=SharpJobs)](http://www.nuget.org/packages/SharpJobs/)
[![SharpJobs.OrmLite](https://img.shields.io/nuget/v/SharpJobs.OrmLite.svg?style=flat-square&label=SharpJobs.OrmLite)](http://www.nuget.org/packages/SharpJobs.OrmLite/)

# Why

Many of the job processers do way more than I need, adding too much risk to my projects. All I need is the ability to store jobs with metadata, and processes them, in code-first way.

## Example

Defining your job:

```csharp
public class MyJob : AbstractJob<MyJobData>
{
    public override Task Run(MyJobData data)
    {
        Console.WriteLine($"Hello {data.Name}, you are {data.Age} years old.");
        return Task.CompletedTask;
    }
}

public class MyJobData
{
    public string Name { get; set; }
    
    public int Age { get; set; }
}
```

Storing and processing your jobs:

```csharp
IJobStorage jobStorage = /**/;
IJobProcessor jobProcessor = /**/;

// Add a job to be ran.
await jobStorage.Enqueue<MyJob, MyJobData>(new MyJobData
{
    Name = "Paul",
    Age = 30
});

// Wire this up however you'd like.
// It's a long running method that looks for and processes jobs.
var cancelToken = new CancellationTokenSource();
jobProcessor.Process(cancelToken.Token);
```