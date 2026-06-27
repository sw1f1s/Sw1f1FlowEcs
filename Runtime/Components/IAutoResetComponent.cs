namespace Sw1f1.FlowEcs.Runtime
{
    public interface IAutoResetComponent<T> where T : struct, IComponent
    {
        public void Reset(ref T c);
    }
}