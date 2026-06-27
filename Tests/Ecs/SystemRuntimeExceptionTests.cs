using System;
using System.Collections.Generic;
using NUnit.Framework;
using Sw1f1.FlowEcs.DI;
using Sw1f1.FlowEcs.Runtime;

namespace Sw1f1.FlowEcs.Tests.Ecs
{
    [TestFixture]
    public sealed class SystemRuntimeExceptionTests
    {
        [Test]
        public void UpdateSystemException_IsReportedAndNextSystemStillRuns()
        {
            var nextSystem = new CountingUpdateSystem();
            var messages = new List<string>();

            using IWorld world = WorldBuilder.Build();
            using var systems = new Systems(world);
            systems
                .Add(new ThrowingUpdateSystem())
                .Add(nextSystem)
                .Inject();

            systems.SystemException += systemException =>
            {
                messages.Add(
                    $"System runtime exception in {systemException.System.GetType().FullName}.{systemException.Stage}. Execution will continue.");
                messages.Add(systemException.Exception.Message);
            };

            systems.Update();

            Assert.That(nextSystem.UpdateCount, Is.EqualTo(1));
            Assert.That(messages.Exists(message => message.Contains("System runtime exception")), Is.True);
            Assert.That(messages.Exists(message => message.Contains(nameof(ThrowingUpdateSystem))), Is.True);
            Assert.That(messages.Exists(message => message.Contains("Execution will continue")), Is.True);
            Assert.That(messages, Has.Some.EqualTo("runtime boom"));
        }

        private sealed class ThrowingUpdateSystem : IUpdateSystem
        {
            public void Update()
            {
                throw new InvalidOperationException("runtime boom");
            }
        }

        private sealed class CountingUpdateSystem : IUpdateSystem
        {
            public int UpdateCount { get; private set; }

            public void Update()
            {
                UpdateCount++;
            }
        }
    }
}
