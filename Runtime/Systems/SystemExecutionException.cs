using System;

namespace Sw1f1.FlowEcs.Runtime
{
    public readonly struct SystemExecutionException
    {
        public SystemExecutionException(Exception exception, ISystem system, string stage)
        {
            Exception = exception;
            System = system;
            Stage = stage;
        }

        public Exception Exception { get; }
        public ISystem System { get; }
        public string Stage { get; }
    }
}
