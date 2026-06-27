namespace Sw1f1.FlowEcs.Runtime
{
    public interface IAutoDestroyComponent<T> where T : struct, IComponent
    {
        public void Destroy(ref T c);
    }
}