using System;

namespace Microsoft.Azure.Functions.Worker
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class TimerTriggerAttribute : Attribute
    {
        public TimerTriggerAttribute(string scheduleExpression)
        {
            ScheduleExpression = scheduleExpression;
        }

        public string ScheduleExpression { get; }
    }
}