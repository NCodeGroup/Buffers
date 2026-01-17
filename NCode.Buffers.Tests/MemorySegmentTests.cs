#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

public class MemorySegmentTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithCharMemory_SetsMemoryProperty()
    {
        var source = "hello".AsMemory();

        var segment = new MemorySegment<char>(source);

        Assert.Equal(source, segment.Memory);
    }

    [Fact]
    public void Constructor_WithByteArray_SetsMemoryProperty()
    {
        byte[] source = [1, 2, 3, 4, 5];

        var segment = new MemorySegment<byte>(source);

        Assert.Equal(source, segment.Memory.ToArray());
    }

    [Fact]
    public void Constructor_SetsRunningIndexToZero()
    {
        var segment = new MemorySegment<char>("test".AsMemory());

        Assert.Equal(0, segment.RunningIndex);
    }

    [Fact]
    public void Constructor_SetsNextToNull()
    {
        var segment = new MemorySegment<char>("test".AsMemory());

        Assert.Null(segment.Next);
    }

    [Fact]
    public void Constructor_WithEmptyMemory_CreatesValidSegment()
    {
        var source = ReadOnlyMemory<char>.Empty;

        var segment = new MemorySegment<char>(source);

        Assert.True(segment.Memory.IsEmpty);
        Assert.Equal(0, segment.Memory.Length);
        Assert.Equal(0, segment.RunningIndex);
        Assert.Null(segment.Next);
    }

    #endregion

    #region Append Tests

    [Fact]
    public void Append_Valid()
    {
        var firstSource = "first".AsMemory();
        var firstSegment = new MemorySegment<char>(firstSource);
        Assert.Null(firstSegment.Next);
        Assert.Equal(firstSource, firstSegment.Memory);
        Assert.Equal(0, firstSegment.RunningIndex);

        var secondSource = "source".AsMemory();
        var secondSegment = firstSegment.Append(secondSource);
        Assert.Equal(secondSegment, firstSegment.Next);
        Assert.Null(secondSegment.Next);
        Assert.Equal(secondSource, secondSegment.Memory);
        Assert.Equal(firstSource.Length, secondSegment.RunningIndex);
    }

    [Fact]
    public void Append_ReturnsNewSegmentWithCorrectMemory()
    {
        var first = new MemorySegment<char>("first".AsMemory());
        var secondSource = "second".AsMemory();

        var second = first.Append(secondSource);

        Assert.Equal(secondSource, second.Memory);
    }

    [Fact]
    public void Append_SetsNextPropertyOnCurrentSegment()
    {
        var first = new MemorySegment<char>("first".AsMemory());

        var second = first.Append("second".AsMemory());

        Assert.Same(second, first.Next);
    }

    [Fact]
    public void Append_NewSegmentHasNullNext()
    {
        var first = new MemorySegment<char>("first".AsMemory());

        var second = first.Append("second".AsMemory());

        Assert.Null(second.Next);
    }

    [Fact]
    public void Append_CalculatesRunningIndexCorrectly()
    {
        var firstSource = "hello".AsMemory(); // Length = 5
        var first = new MemorySegment<char>(firstSource);

        var second = first.Append("world".AsMemory());

        Assert.Equal(5, second.RunningIndex);
    }

    [Fact]
    public void Append_WithEmptyFirstSegment_RunningIndexIsZero()
    {
        var first = new MemorySegment<char>(ReadOnlyMemory<char>.Empty);

        var second = first.Append("test".AsMemory());

        Assert.Equal(0, second.RunningIndex);
    }

    [Fact]
    public void Append_WithEmptyMemory_CreatesValidSegment()
    {
        var first = new MemorySegment<char>("test".AsMemory());

        var second = first.Append(ReadOnlyMemory<char>.Empty);

        Assert.Same(second, first.Next);
        Assert.True(second.Memory.IsEmpty);
        Assert.Equal(4, second.RunningIndex);
    }

    #endregion

    #region Chaining Tests

    [Fact]
    public void Append_MultipleSegments_CreatesLinkedList()
    {
        var first = new MemorySegment<char>("one".AsMemory());

        var second = first.Append("two".AsMemory());
        var third = second.Append("three".AsMemory());

        Assert.Same(second, first.Next);
        Assert.Same(third, second.Next);
        Assert.Null(third.Next);
    }

    [Fact]
    public void Append_MultipleSegments_CalculatesRunningIndexCorrectly()
    {
        var first = new MemorySegment<char>("abc".AsMemory()); // Length = 3

        var second = first.Append("defg".AsMemory()); // Length = 4
        var third = second.Append("hi".AsMemory()); // Length = 2

        Assert.Equal(0, first.RunningIndex);
        Assert.Equal(3, second.RunningIndex); // 0 + 3
        Assert.Equal(7, third.RunningIndex); // 3 + 4
    }

    [Fact]
    public void Append_ManySegments_MaintainsCorrectChain()
    {
        var segments = new List<MemorySegment<byte>>();
        var first = new MemorySegment<byte>((byte[])[1]);
        segments.Add(first);

        var current = first;
        for (var i = 2; i <= 10; i++)
        {
            current = current.Append((byte[])[(byte)i]);
            segments.Add(current);
        }

        Assert.Equal(10, segments.Count);

        var iter = first;
        for (var i = 0; i < 9; i++)
        {
            Assert.NotNull(iter.Next);
            Assert.Same(segments[i + 1], iter.Next);
            iter = (MemorySegment<byte>)iter.Next;
        }

        Assert.Null(iter.Next);
    }

    #endregion

    #region ReadOnlySequence Integration Tests

    [Fact]
    public void CanCreateReadOnlySequence_FromSingleSegment()
    {
        byte[] source = [1, 2, 3, 4, 5];
        var segment = new MemorySegment<byte>(source);

        var sequence = new ReadOnlySequence<byte>(segment, 0, segment, segment.Memory.Length);

        Assert.Equal(5, sequence.Length);
        Assert.True(sequence.IsSingleSegment);
        Assert.Equal(source, sequence.ToArray());
    }

    [Fact]
    public void CanCreateReadOnlySequence_FromMultipleSegments()
    {
        var first = new MemorySegment<byte>((byte[])[1, 2, 3]);
        var last = first.Append((byte[])[4, 5, 6]);

        var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);

        Assert.Equal(6, sequence.Length);
        Assert.False(sequence.IsSingleSegment);
        Assert.Equal((byte[])[1, 2, 3, 4, 5, 6], sequence.ToArray());
    }

    [Fact]
    public void CanCreateReadOnlySequence_FromThreeSegments()
    {
        var first = new MemorySegment<char>("Hello".AsMemory());
        var second = first.Append(" ".AsMemory());
        var last = second.Append("World".AsMemory());

        var sequence = new ReadOnlySequence<char>(first, 0, last, last.Memory.Length);

        Assert.Equal(11, sequence.Length);
        Assert.Equal("Hello World", new string(sequence.ToArray()));
    }

    [Fact]
    public void ReadOnlySequence_CanSliceAcrossSegments()
    {
        var first = new MemorySegment<byte>((byte[])[1, 2, 3]);
        var last = first.Append((byte[])[4, 5, 6]);
        var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);

        var sliced = sequence.Slice(2, 3);

        Assert.Equal(3, sliced.Length);
        Assert.Equal((byte[])[3, 4, 5], sliced.ToArray());
    }

    [Fact]
    public void ReadOnlySequence_WithEmptyMiddleSegment()
    {
        var first = new MemorySegment<byte>((byte[])[1, 2]);
        var middle = first.Append(ReadOnlyMemory<byte>.Empty);
        var last = middle.Append((byte[])[3, 4]);
        var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);

        Assert.Equal(4, sequence.Length);
        Assert.Equal((byte[])[1, 2, 3, 4], sequence.ToArray());
    }

    #endregion

    #region Generic Type Tests

    [Fact]
    public void WorksWithIntType()
    {
        int[] source = [10, 20, 30];

        var segment = new MemorySegment<int>(source);
        var next = segment.Append((int[])[40, 50]);

        Assert.Equal(source, segment.Memory.ToArray());
        Assert.Equal((int[])[40, 50], next.Memory.ToArray());
        Assert.Equal(3, next.RunningIndex);
    }

    [Fact]
    public void WorksWithCustomStructType()
    {
        Point[] source = [new Point(1, 2), new Point(3, 4)];

        var segment = new MemorySegment<Point>(source);

        Assert.Equal(2, segment.Memory.Length);
        Assert.Equal(1, segment.Memory.Span[0].X);
        Assert.Equal(2, segment.Memory.Span[0].Y);
        Assert.Equal(3, segment.Memory.Span[1].X);
        Assert.Equal(4, segment.Memory.Span[1].Y);
    }

    private readonly record struct Point(int X, int Y);

    #endregion

    #region Inheritance Tests

    [Fact]
    public void InheritsFromReadOnlySequenceSegment()
    {
        var segment = new MemorySegment<byte>((byte[])[1, 2, 3]);

        Assert.IsAssignableFrom<ReadOnlySequenceSegment<byte>>(segment);
    }

    [Fact]
    public void BaseClassMemoryProperty_IsAccessible()
    {
        ReadOnlySequenceSegment<char> segment = new MemorySegment<char>("test".AsMemory());

        Assert.Equal("test".AsMemory(), segment.Memory);
    }

    [Fact]
    public void BaseClassNextProperty_IsAccessible()
    {
        var first = new MemorySegment<char>("first".AsMemory());
        var second = first.Append("second".AsMemory());

        ReadOnlySequenceSegment<char> baseFirst = first;

        Assert.Same(second, baseFirst.Next);
    }

    [Fact]
    public void BaseClassRunningIndexProperty_IsAccessible()
    {
        var first = new MemorySegment<char>("first".AsMemory());
        var second = first.Append("second".AsMemory());

        ReadOnlySequenceSegment<char> baseSecond = second;

        Assert.Equal(5, baseSecond.RunningIndex);
    }

    #endregion

    #region Large Data Tests

    [Fact]
    public void WorksWithLargeByteArray()
    {
        var source = new byte[1024 * 1024]; // 1 MB
        for (var i = 0; i < source.Length; i++)
        {
            source[i] = (byte)(i % 256);
        }

        var segment = new MemorySegment<byte>(source);

        Assert.Equal(1024 * 1024, segment.Memory.Length);
        Assert.Equal(source, segment.Memory.ToArray());
    }

    [Fact]
    public void Append_LargeSegments_CalculatesRunningIndexCorrectly()
    {
        const int largeSize = 100_000;
        var first = new MemorySegment<byte>(new byte[largeSize]);

        var second = first.Append(new byte[largeSize]);
        var third = second.Append(new byte[largeSize]);

        Assert.Equal(0, first.RunningIndex);
        Assert.Equal(largeSize, second.RunningIndex);
        Assert.Equal(largeSize * 2, third.RunningIndex);
    }

    #endregion

    #region Boundary Condition Tests

    [Fact]
    public void Append_SingleElementSegments_WorksCorrectly()
    {
        var first = new MemorySegment<byte>((byte[])[1]);

        var second = first.Append((byte[])[2]);
        var third = second.Append((byte[])[3]);

        Assert.Equal(0, first.RunningIndex);
        Assert.Equal(1, second.RunningIndex);
        Assert.Equal(2, third.RunningIndex);

        var sequence = new ReadOnlySequence<byte>(first, 0, third, third.Memory.Length);
        Assert.Equal((byte[])[1, 2, 3], sequence.ToArray());
    }

    [Fact]
    public void ReadOnlySequence_PartialFirstSegment()
    {
        var first = new MemorySegment<byte>((byte[])[1, 2, 3, 4, 5]);
        var last = first.Append((byte[])[6, 7, 8, 9, 10]);

        var sequence = new ReadOnlySequence<byte>(first, 2, last, last.Memory.Length);

        Assert.Equal(8, sequence.Length);
        Assert.Equal((byte[])[3, 4, 5, 6, 7, 8, 9, 10], sequence.ToArray());
    }

    [Fact]
    public void ReadOnlySequence_PartialLastSegment()
    {
        var first = new MemorySegment<byte>((byte[])[1, 2, 3, 4, 5]);
        var last = first.Append((byte[])[6, 7, 8, 9, 10]);

        var sequence = new ReadOnlySequence<byte>(first, 0, last, 3);

        Assert.Equal(8, sequence.Length);
        Assert.Equal((byte[])[1, 2, 3, 4, 5, 6, 7, 8], sequence.ToArray());
    }

    [Fact]
    public void ReadOnlySequence_PartialBothSegments()
    {
        var first = new MemorySegment<byte>((byte[])[1, 2, 3, 4, 5]);
        var last = first.Append((byte[])[6, 7, 8, 9, 10]);

        var sequence = new ReadOnlySequence<byte>(first, 2, last, 3);

        Assert.Equal(6, sequence.Length);
        Assert.Equal((byte[])[3, 4, 5, 6, 7, 8], sequence.ToArray());
    }

    [Fact]
    public void ReadOnlySequence_EmptySequence()
    {
        var segment = new MemorySegment<byte>((byte[])[1, 2, 3]);

        var sequence = new ReadOnlySequence<byte>(segment, 1, segment, 1);

        Assert.Equal(0, sequence.Length);
        Assert.True(sequence.IsEmpty);
    }

    #endregion

    #region SequenceReader Integration Tests

    [Fact]
    public void SequenceReader_CanReadAcrossSegments()
    {
        var first = new MemorySegment<byte>((byte[])[1, 2, 3]);
        var last = first.Append((byte[])[4, 5, 6]);
        var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        var reader = new SequenceReader<byte>(sequence);

        Assert.True(reader.TryRead(out var b1));
        Assert.Equal(1, b1);

        Assert.True(reader.TryRead(out var b2));
        Assert.Equal(2, b2);

        Assert.True(reader.TryRead(out var b3));
        Assert.Equal(3, b3);

        Assert.True(reader.TryRead(out var b4));
        Assert.Equal(4, b4);

        Assert.True(reader.TryRead(out var b5));
        Assert.Equal(5, b5);

        Assert.True(reader.TryRead(out var b6));
        Assert.Equal(6, b6);

        Assert.False(reader.TryRead(out _));
    }

    [Fact]
    public void SequenceReader_AdvanceAcrossSegments()
    {
        var first = new MemorySegment<byte>((byte[])[1, 2, 3]);
        var second = first.Append((byte[])[4, 5, 6]);
        var last = second.Append((byte[])[7, 8, 9]);
        var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        var reader = new SequenceReader<byte>(sequence);

        reader.Advance(7);

        Assert.True(reader.TryRead(out var b));
        Assert.Equal(8, b);
    }

    #endregion

    #region Append Override Behavior Tests

    [Fact]
    public void Append_OverwritesExistingNext()
    {
        var first = new MemorySegment<char>("first".AsMemory());
        var originalSecond = first.Append("second".AsMemory());

        var newSecond = first.Append("new".AsMemory());

        Assert.Same(newSecond, first.Next);
        Assert.NotSame(originalSecond, first.Next);
    }

    [Fact]
    public void Append_DoesNotAffectOriginalSegmentRunningIndex()
    {
        var first = new MemorySegment<char>("first".AsMemory());
        var originalRunningIndex = first.RunningIndex;

        first.Append("second".AsMemory());

        Assert.Equal(originalRunningIndex, first.RunningIndex);
    }

    #endregion
}