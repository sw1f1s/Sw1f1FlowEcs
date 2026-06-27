using System.Collections.Generic;
using Sw1f1.FlowEcs.Collections;

namespace Sw1f1.FlowEcs.Runtime
{
    internal interface IComponentsStorage
    {
        IReadOnlyList<int> OneTickStorages { get; }
        ref SparseArray<IComponentStorage> Storages { get; }
        IComponentStorage Get(int componentId);
    }
}
