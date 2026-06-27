using Sw1f1.FlowEcs.Collections;

namespace Sw1f1.FlowEcs.Runtime
{
    public interface IPoolFactory
    {
        PooledList<T> Rent<T>(int initialCapacity = 4);
    }
}