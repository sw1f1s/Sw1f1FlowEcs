using Sw1f1.FlowEcs.Runtime;

namespace Sw1f1.FlowEcs.DI
{
    public struct SystemsInject : IDataInject
    {
        private ISystems _value;

        public readonly ISystems Value => _value;

        void IDataInject.Fill(ISystems systems)
        {
            _value = systems;
        }
    }
}
