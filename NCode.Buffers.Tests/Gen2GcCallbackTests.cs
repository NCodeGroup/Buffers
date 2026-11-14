#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Runtime.CompilerServices;

namespace NCode.Buffers.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SerialGcTestGroup
{
    public const string Name = "Serial GC tests";
}

[Collection(SerialGcTestGroup.Name)]
public class Gen2GcCallbackTests
{
    private static void CollectUntil(Func<bool> isDone)
    {
        for (var i = 0; i < 10 && !isDone(); i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    private static void Collect(int times)
    {
        for (var i = 0; i < times; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    // Creates the target in a frame of its own, so no local of the calling test keeps it alive.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RegisterWithCollectableTarget(Action onCallback)
    {
        Gen2GcCallback.Register(_ =>
        {
            onCallback();
            return true;
        }, new object());
    }

    #region Register Tests

    [Fact]
    public void Register_WithoutTarget_InvokesCallbackUntilItReturnsFalse()
    {
        var count = 0;

        Gen2GcCallback.Register(() => Interlocked.Increment(ref count) < 2);

        CollectUntil(() => Volatile.Read(ref count) >= 2);
        Collect(2);

        Assert.Equal(2, Volatile.Read(ref count));
    }

    [Fact]
    public void Register_WithTarget_PassesTargetToCallback()
    {
        var target = new object();
        object? received = null;

        Gen2GcCallback.Register(state =>
        {
            received = state;
            return false;
        }, target);

        CollectUntil(() => Volatile.Read(ref received) is not null);

        Assert.Same(target, received);
        GC.KeepAlive(target);
    }

    [Fact]
    public void Register_WithTarget_InvokesCallbackUntilItReturnsFalse()
    {
        var target = new object();
        var count = 0;

        Gen2GcCallback.Register(_ => Interlocked.Increment(ref count) < 3, target);

        CollectUntil(() => Volatile.Read(ref count) >= 3);
        Collect(2);

        Assert.Equal(3, Volatile.Read(ref count));
        GC.KeepAlive(target);
    }

    [Fact]
    public void Register_WhenTargetIsCollected_NeverInvokesCallback()
    {
        var count = 0;

        RegisterWithCollectableTarget(() => Interlocked.Increment(ref count));
        Collect(5);

        Assert.Equal(0, Volatile.Read(ref count));
    }

    #endregion
}
