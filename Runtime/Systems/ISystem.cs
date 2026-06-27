namespace Sw1f1.FlowEcs.Runtime
{
    public interface ISystem { }

    public interface IInitSystem : ISystem
    {
        public void Init();
    }

    public interface IPreUpdateSystem : ISystem
    {
        public void PreUpdate();
    }

    public interface IUpdateSystem : ISystem
    {
        public void Update();
    }

    public interface IFixedUpdateSystem : ISystem
    {
        public void FixedUpdate();
    }

    public interface ILateUpdateSystem : ISystem
    {
        public void LateUpdate();
    }
}
