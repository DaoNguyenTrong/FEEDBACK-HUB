using System.Linq.Expressions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using FeedbackHub.Application.Common.Interfaces;

namespace FeedbackHub.Infrastructure.Services.Jobs;

internal sealed class HangfireJobDispatcher(IBackgroundJobClient backgroundJobClient) : IBackgroundJobDispatcher
{
    public void Enqueue<TJob>(Expression<Func<TJob, Task>> methodCall, string queueName)
    {
        Job job = Job.FromExpression(methodCall);
        backgroundJobClient.Create(job, new EnqueuedState(queueName));
    }
}
