using System;

namespace Sw1f1.FlowEcs.Runtime
{
    public interface IGroupSystem : ISystem
    {
        public string GroupName { get; }
        public bool State { get; }
        public object[] Injects => Array.Empty<object>();
        public ISystem[] Systems { get; }
    }
}
