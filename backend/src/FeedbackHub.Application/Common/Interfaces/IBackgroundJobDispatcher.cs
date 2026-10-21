using System.Linq.Expressions;

namespace FeedbackHub.Application.Common.Interfaces;

public interface IBackgroundJobDispatcher
{
    void Enqueue<TJob>(Expression<Func<TJob, Task>> methodCall, string queueName);
}
