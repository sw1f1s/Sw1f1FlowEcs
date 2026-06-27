namespace Sw1f1.FlowEcs.Runtime
{
    public interface IAutoPoolComponent<T> where T : struct, IComponent
    {
        public void Reset(ref T c, IPoolFactory poolFactory);
        public void Destroy(ref T c, IPoolFactory poolFactory);
    }
}