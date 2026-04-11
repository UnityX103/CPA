using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using APP.Network.System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace APP.Network.Tests
{
    [TestFixture]
    public sealed class MainThreadDispatcherTests
    {
        [Test]
        public void DrainMainThreadQueue_WhenQueuedActionsExist_ProcessesThemInOrder()
        {
            var system = new NetworkSystem();
            ConcurrentQueue<Action> queue = GetQueue(system);
            var executionOrder = new List<int>();

            queue.Enqueue(() => executionOrder.Add(1));
            queue.Enqueue(() => executionOrder.Add(2));
            queue.Enqueue(() => executionOrder.Add(3));

            system.DrainMainThreadQueue();

            Assert.That(executionOrder, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void DrainMainThreadQueue_WhenActionThrows_ContinuesProcessingRemainingActions()
        {
            var system = new NetworkSystem();
            ConcurrentQueue<Action> queue = GetQueue(system);
            var executionOrder = new List<int>();

            queue.Enqueue(() => executionOrder.Add(1));
            queue.Enqueue(() => throw new InvalidOperationException("boom"));
            queue.Enqueue(() => executionOrder.Add(3));

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: boom");
            Assert.DoesNotThrow(system.DrainMainThreadQueue);
            Assert.That(executionOrder, Is.EqualTo(new[] { 1, 3 }));
        }

        private static ConcurrentQueue<Action> GetQueue(NetworkSystem system)
        {
            FieldInfo field = typeof(NetworkSystem).GetField("_mainThreadQueue",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, "NetworkSystem 必须保留 _mainThreadQueue 字段供主线程调度使用。");

            return (ConcurrentQueue<Action>)field.GetValue(system);
        }
    }
}
