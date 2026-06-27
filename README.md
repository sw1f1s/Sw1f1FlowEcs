# Sw1f1 Flow ECS

Sw1f1 Flow ECS is a compact C# Entity Component System runtime for .NET and Unity. It is extracted from Pixagen and shaped as a standalone package with fast sparse component storage, cached filters, chunked filter iteration, and lightweight DI for systems.

## Content

- [Install](#install)
- [API](#api)
  - [Worlds](#worlds)
  - [Components](#components)
  - [PooledList](#pooledlist)
  - [Entities](#entities)
  - [Filters](#filters)
  - [Systems](#systems)
  - [Group Systems](#group-systems)
  - [DI](#di)
- [Unity](#unity)
- [Build And Pack](#build-and-pack)
- [Tests](#tests)
- [Release](#release)

## Install

### NuGet

```bash
dotnet add package Sw1f1.FlowEcs
```

### Unity

Install from a Git URL:

```text
https://github.com/sw1f1s/Sw1f1FlowEcs.git#v1.0.0
```

Or add it to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.sw1f1.flow-ecs": "https://github.com/sw1f1s/Sw1f1FlowEcs.git#v1.0.0"
  }
}
```

## API

Most runtime types live in `Sw1f1.FlowEcs.Runtime`. DI helpers live in `Sw1f1.FlowEcs.DI`. Collection helpers live in `Sw1f1.FlowEcs.Collections`.

```csharp
using Sw1f1.FlowEcs.Collections;
using Sw1f1.FlowEcs.DI;
using Sw1f1.FlowEcs.Runtime;
```

### Worlds

Create a world:

```csharp
IWorld world = WorldBuilder.Build();
```

Destroy a world:

```csharp
world.Dispose();
```

Destroy all active worlds:

```csharp
WorldBuilder.AllDestroy();
```

### Components

Components are structs marked with `IComponent`.

```csharp
public struct Health : IComponent
{
    public int Value;

    public Health(int value)
    {
        Value = value;
    }
}
```

Use `IAutoResetComponent<T>` when a component needs custom initialization for `Set` or `GetOrSet`.

```csharp
public struct Velocity : IComponent, IAutoResetComponent<Velocity>
{
    public float X;
    public float Y;

    public void Reset(ref Velocity component)
    {
        component.X = 1f;
        component.Y = 0f;
    }
}
```

Use `IAutoCopyComponent<T>` to customize data when an entity is copied.

```csharp
public struct Path : IComponent, IAutoCopyComponent<Path>
{
    public List<int>? Points;

    public void Copy(ref Path src, ref Path dst)
    {
        dst.Points = src.Points is null ? null : new List<int>(src.Points);
    }
}
```

Use `IAutoDestroyComponent<T>` to release component-owned data when the component is removed or the entity is destroyed.

```csharp
public struct Inventory : IComponent, IAutoDestroyComponent<Inventory>
{
    public List<int>? Items;

    public void Destroy(ref Inventory component)
    {
        component.Items?.Clear();
    }
}
```

`IOneTickComponent` marks components that should be removed automatically during the `LateUpdate` stage when the world is updated through `Systems`.

```csharp
public struct DamageEvent : IComponent, IOneTickComponent
{
    public int Value;
}
```

### PooledList

`PooledList<T>` is a lightweight list backed by `ArrayPool<T>`. It is useful for temporary component-owned collections, copied component data, or internal buffers where you want to avoid repeated array allocations.

You usually get pooled collections from `IPoolFactory` inside `IAutoPoolComponent<T>`.

```csharp
public struct Targets : IComponent, IAutoPoolComponent<Targets>, IAutoCopyComponent<Targets>
{
    public PooledList<Entity> Value;

    public void Reset(ref Targets component, IPoolFactory poolFactory)
    {
        component.Value = poolFactory.Rent<Entity>(initialCapacity: 8);
    }

    public void Copy(ref Targets src, ref Targets dst)
    {
        dst.Value = src.Value.Copy();
    }

    public void Destroy(ref Targets component, IPoolFactory poolFactory)
    {
        component.Value.Return();
    }
}
```

Basic operations look close to a normal list:

```csharp
ref Targets targets = ref targetsInject.GetOrSet(entity);
targets.Value.Add(enemy);
targets.Value.Remove(enemy);
targets.Value.Clear();

for (int i = 0; i < targets.Value.Count; i++)
{
    Entity target = targets.Value[i];
}
```

Call `Return()` when you own a `PooledList<T>` directly. For components that implement `IAutoPoolComponent<T>`, returning the list from `Destroy` is enough because the ECS calls it when the component is removed or the entity is destroyed.

### Entities

Entities are created with an initial component.

```csharp
Entity entity = world.CreateEntity<Health>();
```

Component access is done through `ComponentInject<T>`. This keeps hot component operations cached by storage type.

```csharp
var health = new ComponentInject<Health>(world);
var velocity = new ComponentInject<Velocity>(world);

ref Health healthComponent = ref health.Get(entity);
healthComponent.Value = 100;

ref Velocity velocityComponent = ref velocity.GetOrSet(entity);
velocityComponent.X = 4f;

bool hasVelocity = velocity.Has(entity);
velocity.Remove(entity);
```

Add or replace a component:

```csharp
velocity.Add(entity, new Velocity { X = 2f, Y = 1f });
velocity.Replace(entity, new Velocity { X = 8f, Y = 0f });
```

Copy or destroy an entity through `WorldInject`.

```csharp
var worldInject = new WorldInject(world);

Entity copy = worldInject.Copy(entity);
worldInject.Destroy(entity);
```

When the last component is removed from an entity, the entity is returned to the world pool.

### Filters

Filters cache entities that match include/exclude component masks.

```csharp
Filter healthFilter = world.GetFilter(new FilterMask<Health>());
Filter movingHealthFilter = world.GetFilter(new FilterMask<Health, Velocity>());
Filter aliveMovingFilter = world.GetFilter(new FilterMask<Health, Velocity>.Exclude<DamageEvent>());
```

Iterate a filter:

```csharp
var health = new ComponentInject<Health>(world);

foreach (Entity entity in healthFilter)
{
    ref Health component = ref health.Get(entity);
    component.Value += 1;
}
```

For larger filters, use chunk iteration. `ForEachChunk` can run work in parallel when the entity count is high enough; `ForEachChunkSequential` always stays on the caller thread.

```csharp
public readonly struct HealChunkJob : IFilterChunkProcessor
{
    private readonly ComponentInject<Health> _health;

    public HealChunkJob(ComponentInject<Health> health)
    {
        _health = health;
    }

    public void Execute(FilterChunk chunk)
    {
        foreach (Entity entity in chunk.Entities)
        {
            _health.Get(entity).Value += 1;
        }
    }
}

healthFilter.ForEachChunk(new HealChunkJob(health));
```

### Systems

Systems are plain classes that implement one or more stage interfaces:

- `IInitSystem`
- `IPreUpdateSystem`
- `IFixedUpdateSystem`
- `IUpdateSystem`
- `ILateUpdateSystem`

```csharp
public sealed class SpawnSystem : IInitSystem
{
    private readonly WorldInject _world = default;
    private readonly ComponentInject<Health> _health = default;

    public void Init()
    {
        Entity entity = _world.Create<Health>();
        _health.Get(entity).Value = 100;
    }
}
```

```csharp
public sealed class DamageSystem : IUpdateSystem
{
    private readonly FilterInject<Include<Health>, Exclude<DamageEvent>> _filter = default;
    private readonly ComponentInject<Health> _health = default;

    public void Update()
    {
        foreach (Entity entity in _filter.Value)
        {
            _health.Get(entity).Value -= 1;
        }
    }
}
```

Create and run a system container:

```csharp
using var systems = new Systems(world);
systems
    .Add(new SpawnSystem())
    .Add(new DamageSystem())
    .Inject();

systems.Init();
systems.Update();
```

Run with fixed update steps:

```csharp
systems.Update(fixedStepCount: 2);
```

System exceptions are reported through `SystemException`. The container continues running other systems after a subscriber handles the exception.

```csharp
systems.SystemException += error =>
{
    Console.WriteLine($"{error.Stage}: {error.System.GetType().Name}");
    Console.WriteLine(error.Exception);
};
```

### Group Systems

Group systems let you bundle systems and toggle them by name.

```csharp
public sealed class GameplaySystems : IGroupSystem
{
    public string GroupName => nameof(GameplaySystems);
    public bool State => true;

    public ISystem[] Systems => new ISystem[]
    {
        new SpawnSystem(),
        new DamageSystem(),
    };
}
```

```csharp
systems.Add(new GameplaySystems()).Inject();
systems.SetActiveGroup(nameof(GameplaySystems), false);
bool active = systems.IsActiveGroup(nameof(GameplaySystems));
```

Groups can provide shared services through `Injects`. These objects become available to systems through `CustomInject<T>`.

```csharp
public sealed class GameplaySystems : IGroupSystem
{
    public string GroupName => nameof(GameplaySystems);
    public bool State => true;

    public object[] Injects => new object[] { new GameConfig() };
    public ISystem[] Systems => new ISystem[] { new DamageSystem() };
}
```

`Injects` are not just passive values. The ECS injector also runs DI inside the objects returned by `Injects`, so a group helper can use `WorldInject`, `ComponentInject<T>`, `FilterInject<...>`, `CustomInject<T>`, and `IAfterInject` just like a system.

```csharp
public sealed class GameplaySystems : IGroupSystem
{
    public string GroupName => nameof(GameplaySystems);
    public bool State => true;

    public object[] Injects => new object[]
    {
        new GameConfig { StartHealth = 100 },
        new SpawnHelper(),
    };

    public ISystem[] Systems => new ISystem[]
    {
        new SpawnSystem(),
    };
}

public sealed class SpawnHelper : IAfterInject
{
    private readonly WorldInject _world = default;
    private readonly ComponentInject<Health> _health = default;
    private readonly CustomInject<GameConfig> _config = default;

    public bool Ready { get; private set; }

    public void AfterInject()
    {
        Ready = _world.Value is not null && _config.Value is not null;
    }

    public Entity Spawn()
    {
        Entity entity = _world.Create<Health>();
        _health.Get(entity).Value = _config.Value.StartHealth;
        return entity;
    }
}

public sealed class SpawnSystem : IInitSystem
{
    private readonly CustomInject<SpawnHelper> _spawn = default;

    public void Init()
    {
        _spawn.Value.Spawn();
    }
}
```

If a group dependency implements `IDisposeInject`, it is disposed when the `Systems` container is disposed.

### DI

The built-in injector fills fields that implement ECS inject interfaces:

- `WorldInject`
- `ComponentInject<T>`
- `FilterInject<Include<...>>`
- `FilterInject<Include<...>, Exclude<...>>`
- `SystemsInject`
- `CustomInject<T>`

```csharp
public sealed class ExampleSystem : IInitSystem, IUpdateSystem
{
    private readonly WorldInject _world = default;
    private readonly SystemsInject _systems = default;
    private readonly ComponentInject<Health> _health = default;
    private readonly FilterInject<Include<Health, Velocity>, Exclude<DamageEvent>> _moving = default;
    private readonly CustomInject<GameConfig> _config = default;

    public void Init()
    {
        Entity entity = _world.Create<Health>();
        _health.Get(entity).Value = _config.Value.StartHealth;
    }

    public void Update()
    {
        foreach (Entity entity in _moving.Value)
        {
            _health.Get(entity).Value += 1;
        }
    }
}
```

Inject custom services:

```csharp
systems
    .Add(new ExampleSystem())
    .Inject(new GameConfig { StartHealth = 100 });
```

Objects can receive injection outside the update loop with `InjectObject`.

```csharp
var helper = systems.InjectObject(new SpawnHelper());
```

Types that implement `IAfterInject` are called after injection. Group-provided services that implement `IDisposeInject` are disposed when the `Systems` container is disposed.

## Unity

The Unity package is configured as:

```text
com.sw1f1.flow-ecs
```

Runtime source code is under `Runtime/` and is compiled through `Sw1f1.FlowEcs.asmdef`. The assembly has `noEngineReferences` enabled, so the ECS runtime does not depend on UnityEngine.

## Build And Pack

```bash
dotnet build -c Release -f netstandard2.1
dotnet build -c Release -f net8.0
dotnet pack -c Release --no-build
```

NuGet packages are written to `artifacts/nuget`.

## Tests

The ECS regression tests live in `Tests/` and can be run with:

```bash
dotnet test Tests/Sw1f1FlowEcs.Tests.csproj -c Release
```

The Unity test assembly is configured by `Tests/Sw1f1.FlowEcs.Tests.asmdef`.

To run package tests in Unity, add the package to the consuming project's `Packages/manifest.json`:

```json
{
  "testables": [
    "com.sw1f1.flow-ecs"
  ]
}
```

## Release

1. Update the version in `Sw1f1FlowEcs.csproj` and `package.json`.
2. Update `CHANGELOG.md`.
3. Create a Git tag, for example `v1.0.0`.
4. Push the tag to GitHub.
5. Publish the NuGet package with the `Publish NuGet` GitHub Action.

Unity users can install a specific release by referencing the Git tag.

## Notes

Snapshots from the older `Sw1f1Ecs` README are not part of this package yet.

## License

MIT
