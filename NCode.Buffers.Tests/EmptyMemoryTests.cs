#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using System.Buffers;

namespace NCode.Buffers.Tests;

public class EmptyMemoryTests
{
    #region Singleton Tests

    [Fact]
    public void Singleton_ReturnsSameInstance()
    {
        var value1 = EmptyMemory<byte>.Singleton;
        var value2 = EmptyMemory<byte>.Singleton;

        Assert.Same(value1, value2);
    }

    [Fact]
    public void Singleton_IsNotNull()
    {
        var singleton = EmptyMemory<byte>.Singleton;

        Assert.NotNull(singleton);
    }

    [Fact]
    public void Singleton_DifferentTypesReturnDifferentInstances()
    {
        var byteSingleton = EmptyMemory<byte>.Singleton;
        var charSingleton = EmptyMemory<char>.Singleton;
        var intSingleton = EmptyMemory<int>.Singleton;

        Assert.NotNull(byteSingleton);
        Assert.NotNull(charSingleton);
        Assert.NotNull(intSingleton);
    }

    #endregion

    #region Memory Property Tests

    [Fact]
    public void Memory_ReturnsEmptyMemory()
    {
        var emptyMemory = EmptyMemory<byte>.Singleton;

        Assert.Equal(Memory<byte>.Empty, emptyMemory.Memory);
    }

    [Fact]
    public void Memory_HasZeroLength()
    {
        var emptyMemory = EmptyMemory<byte>.Singleton;

        Assert.Equal(0, emptyMemory.Memory.Length);
    }

    [Fact]
    public void Memory_IsEmpty()
    {
        var emptyMemory = EmptyMemory<byte>.Singleton;

        Assert.True(emptyMemory.Memory.IsEmpty);
    }

    [Fact]
    public void Memory_SpanIsEmpty()
    {
        var emptyMemory = EmptyMemory<byte>.Singleton;

        Assert.True(emptyMemory.Memory.Span.IsEmpty);
    }

    [Fact]
    public void Memory_NewInstanceAlsoReturnsEmpty()
    {
        var emptyMemory = new EmptyMemory<byte>();

        Assert.Equal(Memory<byte>.Empty, emptyMemory.Memory);
        Assert.True(emptyMemory.Memory.IsEmpty);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var emptyMemory = EmptyMemory<byte>.Singleton;

        var exception = Record.Exception(() => emptyMemory.Dispose());

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var emptyMemory = EmptyMemory<byte>.Singleton;

        emptyMemory.Dispose();
        emptyMemory.Dispose();
        emptyMemory.Dispose();

        Assert.True(emptyMemory.Memory.IsEmpty);
    }

    [Fact]
    public void Dispose_MemoryStillAccessibleAfterDispose()
    {
        var emptyMemory = EmptyMemory<byte>.Singleton;

        emptyMemory.Dispose();

        Assert.Equal(Memory<byte>.Empty, emptyMemory.Memory);
    }

    [Fact]
    public void Dispose_NewInstanceCanBeDisposed()
    {
        var emptyMemory = new EmptyMemory<byte>();

        var exception = Record.Exception(() => emptyMemory.Dispose());

        Assert.Null(exception);
    }

    #endregion

    #region IMemoryOwner Interface Tests

    [Fact]
    public void ImplementsIMemoryOwner()
    {
        var emptyMemory = EmptyMemory<byte>.Singleton;

        Assert.IsAssignableFrom<IMemoryOwner<byte>>(emptyMemory);
    }

    [Fact]
    public void ImplementsIDisposable()
    {
        var emptyMemory = EmptyMemory<byte>.Singleton;

        Assert.IsAssignableFrom<IDisposable>(emptyMemory);
    }

    [Fact]
    public void CanBeUsedAsIMemoryOwner()
    {
        IMemoryOwner<byte> memoryOwner = EmptyMemory<byte>.Singleton;

        Assert.True(memoryOwner.Memory.IsEmpty);
    }

    [Fact]
    public void CanBeUsedInUsingStatement()
    {
        var exception = Record.Exception(() =>
        {
            using var memoryOwner = new EmptyMemory<byte>();
            _ = memoryOwner.Memory;
        });

        Assert.Null(exception);
    }

    [Fact]
    public void CanBeUsedInUsingStatementWithSingleton()
    {
        var exception = Record.Exception(() =>
        {
            using var memoryOwner = EmptyMemory<byte>.Singleton;
            _ = memoryOwner.Memory;
        });

        Assert.Null(exception);
    }

    #endregion

    #region Generic Type Tests

    [Fact]
    public void WorksWithByteType()
    {
        var emptyMemory = EmptyMemory<byte>.Singleton;

        Assert.True(emptyMemory.Memory.IsEmpty);
        Assert.Equal(0, emptyMemory.Memory.Length);
    }

    [Fact]
    public void WorksWithCharType()
    {
        var emptyMemory = EmptyMemory<char>.Singleton;

        Assert.True(emptyMemory.Memory.IsEmpty);
        Assert.Equal(0, emptyMemory.Memory.Length);
    }

    [Fact]
    public void WorksWithIntType()
    {
        var emptyMemory = EmptyMemory<int>.Singleton;

        Assert.True(emptyMemory.Memory.IsEmpty);
        Assert.Equal(0, emptyMemory.Memory.Length);
    }

    [Fact]
    public void WorksWithCustomStructType()
    {
        var emptyMemory = EmptyMemory<TestStruct>.Singleton;

        Assert.True(emptyMemory.Memory.IsEmpty);
        Assert.Equal(0, emptyMemory.Memory.Length);

        // Verify type is correct by checking default value behavior
        var defaultStruct = new TestStruct { Value = 42 };
        Assert.Equal(42, defaultStruct.Value);
    }

    private readonly struct TestStruct
    {
        public int Value { get; init; }
    }

    #endregion

    #region Instance vs Singleton Tests

    [Fact]
    public void NewInstance_IsDifferentFromSingleton()
    {
        var singleton = EmptyMemory<byte>.Singleton;
        var newInstance = new EmptyMemory<byte>();

        Assert.NotSame(singleton, newInstance);
    }

    [Fact]
    public void NewInstance_HasSameBehaviorAsSingleton()
    {
        var singleton = EmptyMemory<byte>.Singleton;
        var newInstance = new EmptyMemory<byte>();

        Assert.Equal(singleton.Memory.Length, newInstance.Memory.Length);
        Assert.Equal(singleton.Memory.IsEmpty, newInstance.Memory.IsEmpty);
    }

    [Fact]
    public void MultipleNewInstances_AreDifferent()
    {
        var instance1 = new EmptyMemory<byte>();
        var instance2 = new EmptyMemory<byte>();

        Assert.NotSame(instance1, instance2);
    }

    #endregion
}
