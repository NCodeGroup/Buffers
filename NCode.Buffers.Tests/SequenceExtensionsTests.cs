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

using Nerdbank.Streams;

namespace NCode.Buffers.Tests;

public class SequenceExtensionsTests
{
    #region GetSpanLease Tests

    [Fact]
    public void GetSpanLease_EmptySequence_ReturnsEmptySpan()
    {
        using var sequence = new Sequence<byte>();

        using var lease = sequence.GetSpanLease(isSensitive: false);

        Assert.True(lease.Span.IsEmpty);
        Assert.Equal(0, lease.Span.Length);
    }

    [Fact]
    public void GetSpanLease_EmptySequence_Sensitive_ReturnsEmptySpan()
    {
        using var sequence = new Sequence<byte>();

        using var lease = sequence.GetSpanLease(isSensitive: true);

        Assert.True(lease.Span.IsEmpty);
        Assert.Equal(0, lease.Span.Length);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetSpanLease_SingleSegment_ReturnsSpanDirectly(bool isSensitive)
    {
        using var sequence = new Sequence<byte>();

        var span = sequence.GetSpan(10);
        for (var i = 0; i < 10; i++)
        {
            span[i] = (byte)(i + 1);
        }

        sequence.Advance(10);

        using var lease = sequence.GetSpanLease(isSensitive);

        Assert.Equal(10, lease.Span.Length);
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal((byte)(i + 1), lease.Span[i]);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetSpanLease_MultipleSegments_CopiesDataToContiguousBuffer(bool isSensitive)
    {
        // Use SecureMemoryPool which allocates in PageSize (4096) chunks
        // Write more than one page to force multiple segments
        using var sequence = new Sequence<byte>(SecureMemoryPool<byte>.Shared);

        const int pageSize = SecureMemoryPool<byte>.PageSize;
        var totalSize = pageSize + 100; // More than one page

        // Fill first page
        var span1 = sequence.GetSpan(pageSize);
        for (var i = 0; i < pageSize; i++)
        {
            span1[i] = (byte)(i % 256);
        }

        sequence.Advance(pageSize);

        // Fill second segment
        var span2 = sequence.GetSpan(100);
        for (var i = 0; i < 100; i++)
        {
            span2[i] = (byte)((pageSize + i) % 256);
        }

        sequence.Advance(100);

        // Verify we have multiple segments
        var readOnlySequence = sequence.AsReadOnlySequence;
        Assert.False(readOnlySequence.IsSingleSegment);

        using var lease = sequence.GetSpanLease(isSensitive);

        Assert.Equal(totalSize, lease.Span.Length);

        // Verify data integrity
        for (var i = 0; i < totalSize; i++)
        {
            Assert.Equal((byte)(i % 256), lease.Span[i]);
        }
    }

    [Fact]
    public void GetSpanLease_SingleSegment_SpanMatchesSequenceData()
    {
        using var sequence = new Sequence<byte>();

        var testData = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var span = sequence.GetSpan(testData.Length);
        testData.CopyTo(span);
        sequence.Advance(testData.Length);

        using var lease = sequence.GetSpanLease(isSensitive: true);

        Assert.Equal(testData.Length, lease.Span.Length);
        Assert.True(lease.Span.SequenceEqual(testData));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(4096)] // Exactly one page
    public void GetSpanLease_VariousSizes_ReturnsCorrectLength(int size)
    {
        using var sequence = new Sequence<byte>();

        var span = sequence.GetSpan(size);
        span[..size].Fill(0xFF);
        sequence.Advance(size);

        using var lease = sequence.GetSpanLease(isSensitive: false);

        Assert.Equal(size, lease.Span.Length);
    }

    [Fact]
    public void GetSpanLease_WithSecureMemoryPool_UsesSecureMemory()
    {
        using var sequence = new Sequence<byte>(SecureMemoryPool<byte>.Shared);

        var testData = new byte[] { 1, 2, 3, 4, 5 };
        var span = sequence.GetSpan(testData.Length);
        testData.CopyTo(span);
        sequence.Advance(testData.Length);

        using var lease = sequence.GetSpanLease(isSensitive: true);

        Assert.Equal(testData.Length, lease.Span.Length);
        Assert.True(lease.Span.SequenceEqual(testData));
    }

    [Fact]
    public void GetSpanLease_SingleSegment_Dispose_DoesNotThrow()
    {
        using var sequence = new Sequence<byte>();

        var span = sequence.GetSpan(10);
        span[..10].Fill(0xAB);
        sequence.Advance(10);

        var lease = sequence.GetSpanLease(isSensitive: false);

        // Single segment returns sequence as owner, so dispose should not throw
        lease.Dispose();
    }

    [Fact]
    public void GetSpanLease_MultipleSegments_Dispose_ReleasesRentedBuffer()
    {
        using var sequence = new Sequence<byte>(SecureMemoryPool<byte>.Shared);

        // Force multiple segments by writing more than one page
        const int pageSize = SecureMemoryPool<byte>.PageSize;
        var totalSize = pageSize + 100;

        var span1 = sequence.GetSpan(pageSize);
        span1[..pageSize].Fill(0x01);
        sequence.Advance(pageSize);

        var span2 = sequence.GetSpan(100);
        span2[..100].Fill(0x02);
        sequence.Advance(100);

        // Verify we have multiple segments
        var readOnlySequence = sequence.AsReadOnlySequence;
        Assert.False(readOnlySequence.IsSingleSegment);

        var lease = sequence.GetSpanLease(isSensitive: true);
        Assert.Equal(totalSize, lease.Span.Length);

        // Dispose should not throw and should release the rented buffer
        lease.Dispose();
    }

    [Fact]
    public void GetSpanLease_GenericType_Int32_Works()
    {
        using var sequence = new Sequence<int>();

        var testData = new[] { 100, 200, 300, 400, 500 };
        var span = sequence.GetSpan(testData.Length);
        testData.CopyTo(span);
        sequence.Advance(testData.Length);

        using var lease = sequence.GetSpanLease(isSensitive: false);

        Assert.Equal(testData.Length, lease.Span.Length);
        Assert.True(lease.Span.SequenceEqual(testData));
    }

    [Fact]
    public void GetSpanLease_GenericType_Char_Works()
    {
        using var sequence = new Sequence<char>();

        var testData = "Hello".ToCharArray();
        var span = sequence.GetSpan(testData.Length);
        testData.CopyTo(span);
        sequence.Advance(testData.Length);

        using var lease = sequence.GetSpanLease(isSensitive: false);

        Assert.Equal(testData.Length, lease.Span.Length);
        Assert.True(lease.Span.SequenceEqual(testData));
    }

    [Fact]
    public void GetSpanLease_GenericType_Double_Works()
    {
        using var sequence = new Sequence<double>();

        var testData = new[] { 1.1, 2.2, 3.3, 4.4, 5.5 };
        var span = sequence.GetSpan(testData.Length);
        testData.CopyTo(span);
        sequence.Advance(testData.Length);

        using var lease = sequence.GetSpanLease(isSensitive: true);

        Assert.Equal(testData.Length, lease.Span.Length);
        Assert.True(lease.Span.SequenceEqual(testData));
    }

    [Fact]
    public void GetSpanLease_Sensitive_True_MultipleSegments_UsesSecurePool()
    {
        using var sequence = new Sequence<byte>(SecureMemoryPool<byte>.Shared);

        // Force multiple segments to test CryptoPool usage with isSensitive=true
        const int pageSize = SecureMemoryPool<byte>.PageSize;
        var totalSize = pageSize + 100;

        var data = new byte[totalSize];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 256);
        }

        var span1 = sequence.GetSpan(pageSize);
        data.AsSpan(0, pageSize).CopyTo(span1);
        sequence.Advance(pageSize);

        var span2 = sequence.GetSpan(100);
        data.AsSpan(pageSize, 100).CopyTo(span2);
        sequence.Advance(100);

        // Verify the sequence has multiple segments
        var readOnlySequence = sequence.AsReadOnlySequence;
        Assert.False(readOnlySequence.IsSingleSegment);

        using var lease = sequence.GetSpanLease(isSensitive: true);

        Assert.Equal(data.Length, lease.Span.Length);
        Assert.True(lease.Span.SequenceEqual(data));
    }

    [Fact]
    public void GetSpanLease_Sensitive_False_MultipleSegments_UsesStandardPool()
    {
        using var sequence = new Sequence<byte>(SecureMemoryPool<byte>.Shared);

        // Force multiple segments
        const int pageSize = SecureMemoryPool<byte>.PageSize;
        var totalSize = pageSize + 100;

        var data = new byte[totalSize];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 256);
        }

        var span1 = sequence.GetSpan(pageSize);
        data.AsSpan(0, pageSize).CopyTo(span1);
        sequence.Advance(pageSize);

        var span2 = sequence.GetSpan(100);
        data.AsSpan(pageSize, 100).CopyTo(span2);
        sequence.Advance(100);

        // Verify the sequence has multiple segments
        var readOnlySequence = sequence.AsReadOnlySequence;
        Assert.False(readOnlySequence.IsSingleSegment);

        using var lease = sequence.GetSpanLease(isSensitive: false);

        Assert.Equal(data.Length, lease.Span.Length);
        Assert.True(lease.Span.SequenceEqual(data));
    }

    [Fact]
    public void GetSpanLease_SingleSegment_ReturnsFirstSpanDirectly()
    {
        using var sequence = new Sequence<byte>();

        var testData = new byte[] { 1, 2, 3, 4, 5 };
        var span = sequence.GetSpan(testData.Length);
        testData.CopyTo(span);
        sequence.Advance(testData.Length);

        // Verify single segment
        var readOnlySequence = sequence.AsReadOnlySequence;
        Assert.True(readOnlySequence.IsSingleSegment);

        using var lease = sequence.GetSpanLease(isSensitive: false);

        // The span should be the same as First.Span
        Assert.Equal(readOnlySequence.First.Span.Length, lease.Span.Length);
        Assert.True(lease.Span.SequenceEqual(readOnlySequence.First.Span));
    }

    [Fact]
    public void GetSpanLease_MultipleSegments_PreservesDataOrder()
    {
        using var sequence = new Sequence<byte>(SecureMemoryPool<byte>.Shared);

        const int pageSize = SecureMemoryPool<byte>.PageSize;

        // Write distinct patterns to each segment
        var span1 = sequence.GetSpan(pageSize);
        span1[..pageSize].Fill(0xAA);
        sequence.Advance(pageSize);

        var span2 = sequence.GetSpan(pageSize);
        span2[..pageSize].Fill(0xBB);
        sequence.Advance(pageSize);

        var span3 = sequence.GetSpan(100);
        span3[..100].Fill(0xCC);
        sequence.Advance(100);

        // Verify multiple segments
        Assert.False(sequence.AsReadOnlySequence.IsSingleSegment);

        using var lease = sequence.GetSpanLease(isSensitive: true);

        // Verify the order is preserved
        Assert.Equal(pageSize * 2 + 100, lease.Span.Length);

        // First segment should be 0xAA
        for (var i = 0; i < pageSize; i++)
        {
            Assert.Equal(0xAA, lease.Span[i]);
        }

        // Second segment should be 0xBB
        for (var i = pageSize; i < pageSize * 2; i++)
        {
            Assert.Equal(0xBB, lease.Span[i]);
        }

        // Third segment should be 0xCC
        for (var i = pageSize * 2; i < pageSize * 2 + 100; i++)
        {
            Assert.Equal(0xCC, lease.Span[i]);
        }
    }

    [Fact]
    public void GetSpanLease_SingleSegment_SequenceAsOwner_DisposeIsSafe()
    {
        using var sequence = new Sequence<byte>();

        var span = sequence.GetSpan(5);
        span[..5].Fill(0x12);
        sequence.Advance(5);

        // Single segment case: sequence is passed as the owner
        var lease = sequence.GetSpanLease(isSensitive: false);

        // Multiple disposes should be safe
        lease.Dispose();
        lease.Dispose();
    }

    [Fact]
    public void GetSpanLease_LargeMultipleSegments_HandlesCorrectly()
    {
        using var sequence = new Sequence<byte>(SecureMemoryPool<byte>.Shared);

        const int pageSize = SecureMemoryPool<byte>.PageSize;
        const int totalPages = 5;
        var totalSize = pageSize * totalPages;

        // Write multiple pages
        for (var page = 0; page < totalPages; page++)
        {
            var span = sequence.GetSpan(pageSize);
            span[..pageSize].Fill((byte)(page + 1));
            sequence.Advance(pageSize);
        }

        // Verify multiple segments
        Assert.False(sequence.AsReadOnlySequence.IsSingleSegment);

        using var lease = sequence.GetSpanLease(isSensitive: true);

        Assert.Equal(totalSize, lease.Span.Length);

        // Verify each page has the correct fill value
        for (var page = 0; page < totalPages; page++)
        {
            for (var i = 0; i < pageSize; i++)
            {
                Assert.Equal((byte)(page + 1), lease.Span[page * pageSize + i]);
            }
        }
    }

    [Fact]
    public void GetSpanLease_SingleByte_Works()
    {
        using var sequence = new Sequence<byte>();

        var span = sequence.GetSpan(1);
        span[0] = 0x42;
        sequence.Advance(1);

        using var lease = sequence.GetSpanLease(isSensitive: true);

        Assert.Equal(1, lease.Span.Length);
        Assert.Equal(0x42, lease.Span[0]);
    }

    [Fact]
    public void GetSpanLease_ExactlyOnePage_IsSingleSegment()
    {
        using var sequence = new Sequence<byte>(SecureMemoryPool<byte>.Shared);

        const int pageSize = SecureMemoryPool<byte>.PageSize;

        var span = sequence.GetSpan(pageSize);
        for (var i = 0; i < pageSize; i++)
        {
            span[i] = (byte)(i % 256);
        }

        sequence.Advance(pageSize);

        // Should be single segment
        Assert.True(sequence.AsReadOnlySequence.IsSingleSegment);

        using var lease = sequence.GetSpanLease(isSensitive: false);

        Assert.Equal(pageSize, lease.Span.Length);
    }

    #endregion

    #region ConsumeAsContiguousSpan Tests

    [Fact]
    public void ConsumeAsContiguousSpan_EmptySequence_ReturnsEmptySpan()
    {
        var sequence = new Sequence<byte>();

        var owner = sequence.ConsumeAsContiguousSpan(isSensitive: false, out var span);

        Assert.True(span.IsEmpty);
        Assert.Equal(0, span.Length);

        owner.Dispose();
    }

    [Fact]
    public void ConsumeAsContiguousSpan_EmptySequence_Sensitive_ReturnsEmptySpan()
    {
        var sequence = new Sequence<byte>();

        var owner = sequence.ConsumeAsContiguousSpan(isSensitive: true, out var span);

        Assert.True(span.IsEmpty);
        Assert.Equal(0, span.Length);

        owner.Dispose();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConsumeAsContiguousSpan_SingleSegment_ReturnsSpanDirectly(bool isSensitive)
    {
        var sequence = new Sequence<byte>();

        var writeSpan = sequence.GetSpan(10);
        for (var i = 0; i < 10; i++)
        {
            writeSpan[i] = (byte)(i + 1);
        }

        sequence.Advance(10);

        var owner = sequence.ConsumeAsContiguousSpan(isSensitive, out var span);

        Assert.Equal(10, span.Length);
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal((byte)(i + 1), span[i]);
        }

        owner.Dispose();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConsumeAsContiguousSpan_MultipleSegments_CopiesDataToContiguousBuffer(bool isSensitive)
    {
        var sequence = new Sequence<byte>(SecureMemoryPool<byte>.Shared);

        const int pageSize = SecureMemoryPool<byte>.PageSize;
        var totalSize = pageSize + 100;

        var span1 = sequence.GetSpan(pageSize);
        for (var i = 0; i < pageSize; i++)
        {
            span1[i] = (byte)(i % 256);
        }

        sequence.Advance(pageSize);

        var span2 = sequence.GetSpan(100);
        for (var i = 0; i < 100; i++)
        {
            span2[i] = (byte)((pageSize + i) % 256);
        }

        sequence.Advance(100);

        var readOnlySequence = sequence.AsReadOnlySequence;
        Assert.False(readOnlySequence.IsSingleSegment);

        var owner = sequence.ConsumeAsContiguousSpan(isSensitive, out var span);

        Assert.Equal(totalSize, span.Length);

        for (var i = 0; i < totalSize; i++)
        {
            Assert.Equal((byte)(i % 256), span[i]);
        }

        owner.Dispose();
    }

    [Fact]
    public void ConsumeAsContiguousSpan_SingleSegment_ReturnsSequenceAsOwner()
    {
        var sequence = new Sequence<byte>();

        var testData = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var writeSpan = sequence.GetSpan(testData.Length);
        testData.CopyTo(writeSpan);
        sequence.Advance(testData.Length);

        var owner = sequence.ConsumeAsContiguousSpan(isSensitive: true, out var span);

        // For single segment, the owner should be the sequence itself
        Assert.Same(sequence, owner);
        Assert.Equal(testData.Length, span.Length);
        Assert.True(span.SequenceEqual(testData));

        owner.Dispose();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(4096)]
    public void ConsumeAsContiguousSpan_VariousSizes_ReturnsCorrectLength(int size)
    {
        var sequence = new Sequence<byte>();

        var writeSpan = sequence.GetSpan(size);
        writeSpan[..size].Fill(0xFF);
        sequence.Advance(size);

        var owner = sequence.ConsumeAsContiguousSpan(isSensitive: false, out var span);

        Assert.Equal(size, span.Length);

        owner.Dispose();
    }

    [Fact]
    public void ConsumeAsContiguousSpan_MultipleSegments_DisposesOriginalSequence()
    {
        var sequence = new Sequence<byte>(SecureMemoryPool<byte>.Shared);

        const int pageSize = SecureMemoryPool<byte>.PageSize;
        var totalSize = pageSize + 100;

        var span1 = sequence.GetSpan(pageSize);
        span1[..pageSize].Fill(0x01);
        sequence.Advance(pageSize);

        var span2 = sequence.GetSpan(100);
        span2[..100].Fill(0x02);
        sequence.Advance(100);

        Assert.False(sequence.AsReadOnlySequence.IsSingleSegment);

        var owner = sequence.ConsumeAsContiguousSpan(isSensitive: true, out var span);

        Assert.Equal(totalSize, span.Length);

        // The original sequence should be disposed (length should be 0)
        Assert.Equal(0, sequence.Length);

        owner.Dispose();
    }

    [Fact]
    public void ConsumeAsContiguousSpan_GenericType_Int32_Works()
    {
        var sequence = new Sequence<int>();

        var testData = new[] { 100, 200, 300, 400, 500 };
        var writeSpan = sequence.GetSpan(testData.Length);
        testData.CopyTo(writeSpan);
        sequence.Advance(testData.Length);

        var owner = sequence.ConsumeAsContiguousSpan(isSensitive: false, out var span);

        Assert.Equal(testData.Length, span.Length);
        Assert.True(span.SequenceEqual(testData));

        owner.Dispose();
    }

    [Fact]
    public void ConsumeAsContiguousSpan_GenericType_Char_Works()
    {
        var sequence = new Sequence<char>();

        var testData = "Hello".ToCharArray();
        var writeSpan = sequence.GetSpan(testData.Length);
        testData.CopyTo(writeSpan);
        sequence.Advance(testData.Length);

        var owner = sequence.ConsumeAsContiguousSpan(isSensitive: false, out var span);

        Assert.Equal(testData.Length, span.Length);
        Assert.True(span.SequenceEqual(testData));

        owner.Dispose();
    }

    [Fact]
    public void ConsumeAsContiguousSpan_GenericType_Double_Works()
    {
        var sequence = new Sequence<double>();

        var testData = new[] { 1.1, 2.2, 3.3, 4.4, 5.5 };
        var writeSpan = sequence.GetSpan(testData.Length);
        testData.CopyTo(writeSpan);
        sequence.Advance(testData.Length);

        var owner = sequence.ConsumeAsContiguousSpan(isSensitive: true, out var span);

        Assert.Equal(testData.Length, span.Length);
        Assert.True(span.SequenceEqual(testData));

        owner.Dispose();
    }

    [Fact]
    public void ConsumeAsContiguousSpan_MultipleSegments_PreservesDataOrder()
    {
        var sequence = new Sequence<byte>(SecureMemoryPool<byte>.Shared);

        const int pageSize = SecureMemoryPool<byte>.PageSize;

        var span1 = sequence.GetSpan(pageSize);
        span1[..pageSize].Fill(0xAA);
        sequence.Advance(pageSize);

        var span2 = sequence.GetSpan(pageSize);
        span2[..pageSize].Fill(0xBB);
        sequence.Advance(pageSize);

        var span3 = sequence.GetSpan(100);
        span3[..100].Fill(0xCC);
        sequence.Advance(100);

        Assert.False(sequence.AsReadOnlySequence.IsSingleSegment);

        var owner = sequence.ConsumeAsContiguousSpan(isSensitive: true, out var span);

        Assert.Equal(pageSize * 2 + 100, span.Length);

        for (var i = 0; i < pageSize; i++)
        {
            Assert.Equal(0xAA, span[i]);
        }

        for (var i = pageSize; i < pageSize * 2; i++)
        {
            Assert.Equal(0xBB, span[i]);
        }

        for (var i = pageSize * 2; i < pageSize * 2 + 100; i++)
        {
            Assert.Equal(0xCC, span[i]);
        }

        owner.Dispose();
    }

    [Fact]
    public void ConsumeAsContiguousSpan_LargeMultipleSegments_HandlesCorrectly()
    {
        var sequence = new Sequence<byte>(SecureMemoryPool<byte>.Shared);

        const int pageSize = SecureMemoryPool<byte>.PageSize;
        const int totalPages = 5;
        var totalSize = pageSize * totalPages;

        for (var page = 0; page < totalPages; page++)
        {
            var writeSpan = sequence.GetSpan(pageSize);
            writeSpan[..pageSize].Fill((byte)(page + 1));
            sequence.Advance(pageSize);
        }

        Assert.False(sequence.AsReadOnlySequence.IsSingleSegment);

        var owner = sequence.ConsumeAsContiguousSpan(isSensitive: true, out var span);

        Assert.Equal(totalSize, span.Length);

        for (var page = 0; page < totalPages; page++)
        {
            for (var i = 0; i < pageSize; i++)
            {
                Assert.Equal((byte)(page + 1), span[page * pageSize + i]);
            }
        }

        owner.Dispose();
    }

    [Fact]
    public void ConsumeAsContiguousSpan_SingleByte_Works()
    {
        var sequence = new Sequence<byte>();

        var writeSpan = sequence.GetSpan(1);
        writeSpan[0] = 0x42;
        sequence.Advance(1);

        var owner = sequence.ConsumeAsContiguousSpan(isSensitive: true, out var span);

        Assert.Equal(1, span.Length);
        Assert.Equal(0x42, span[0]);

        owner.Dispose();
    }

    [Fact]
    public void ConsumeAsContiguousSpan_ExactlyOnePage_IsSingleSegment()
    {
        var sequence = new Sequence<byte>(SecureMemoryPool<byte>.Shared);

        const int pageSize = SecureMemoryPool<byte>.PageSize;

        var writeSpan = sequence.GetSpan(pageSize);
        for (var i = 0; i < pageSize; i++)
        {
            writeSpan[i] = (byte)(i % 256);
        }

        sequence.Advance(pageSize);

        Assert.True(sequence.AsReadOnlySequence.IsSingleSegment);

        var owner = sequence.ConsumeAsContiguousSpan(isSensitive: false, out var span);

        Assert.Equal(pageSize, span.Length);

        owner.Dispose();
    }

    [Fact]
    public void ConsumeAsContiguousSpan_OwnerDispose_DoesNotThrow()
    {
        var sequence = new Sequence<byte>();

        var writeSpan = sequence.GetSpan(10);
        writeSpan[..10].Fill(0xAB);
        sequence.Advance(10);

        var owner = sequence.ConsumeAsContiguousSpan(isSensitive: false, out _);

        // Dispose should not throw
        owner.Dispose();
    }

    [Fact]
    public void ConsumeAsContiguousSpan_MultipleSegments_OwnerDispose_ReleasesRentedBuffer()
    {
        var sequence = new Sequence<byte>(SecureMemoryPool<byte>.Shared);

        const int pageSize = SecureMemoryPool<byte>.PageSize;

        var span1 = sequence.GetSpan(pageSize);
        span1[..pageSize].Fill(0x01);
        sequence.Advance(pageSize);

        var span2 = sequence.GetSpan(100);
        span2[..100].Fill(0x02);
        sequence.Advance(100);

        Assert.False(sequence.AsReadOnlySequence.IsSingleSegment);

        var owner = sequence.ConsumeAsContiguousSpan(isSensitive: true, out var span);
        Assert.Equal(pageSize + 100, span.Length);

        // Dispose should not throw and should release the rented buffer
        owner.Dispose();
    }

    [Fact]
    public void ConsumeAsContiguousSpan_SingleSegment_SequenceRemainsAccessibleViaOwner()
    {
        var sequence = new Sequence<byte>();

        var testData = new byte[] { 1, 2, 3, 4, 5 };
        var writeSpan = sequence.GetSpan(testData.Length);
        testData.CopyTo(writeSpan);
        sequence.Advance(testData.Length);

        var owner = sequence.ConsumeAsContiguousSpan(isSensitive: false, out var span);

        // For single segment, sequence is returned as owner, so it should still be accessible
        Assert.Equal(testData.Length, sequence.Length);
        Assert.True(span.SequenceEqual(testData));

        owner.Dispose();
    }

    [Fact]
    public void ConsumeAsContiguousSpan_Sensitive_True_MultipleSegments_UsesSecurePool()
    {
        var sequence = new Sequence<byte>(SecureMemoryPool<byte>.Shared);

        const int pageSize = SecureMemoryPool<byte>.PageSize;
        var totalSize = pageSize + 100;

        var data = new byte[totalSize];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 256);
        }

        var span1 = sequence.GetSpan(pageSize);
        data.AsSpan(0, pageSize).CopyTo(span1);
        sequence.Advance(pageSize);

        var span2 = sequence.GetSpan(100);
        data.AsSpan(pageSize, 100).CopyTo(span2);
        sequence.Advance(100);

        Assert.False(sequence.AsReadOnlySequence.IsSingleSegment);

        var owner = sequence.ConsumeAsContiguousSpan(isSensitive: true, out var span);

        Assert.Equal(data.Length, span.Length);
        Assert.True(span.SequenceEqual(data));

        owner.Dispose();
    }

    [Fact]
    public void ConsumeAsContiguousSpan_Sensitive_False_MultipleSegments_UsesStandardPool()
    {
        var sequence = new Sequence<byte>(SecureMemoryPool<byte>.Shared);

        const int pageSize = SecureMemoryPool<byte>.PageSize;
        var totalSize = pageSize + 100;

        var data = new byte[totalSize];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 256);
        }

        var span1 = sequence.GetSpan(pageSize);
        data.AsSpan(0, pageSize).CopyTo(span1);
        sequence.Advance(pageSize);

        var span2 = sequence.GetSpan(100);
        data.AsSpan(pageSize, 100).CopyTo(span2);
        sequence.Advance(100);

        Assert.False(sequence.AsReadOnlySequence.IsSingleSegment);

        var owner = sequence.ConsumeAsContiguousSpan(isSensitive: false, out var span);

        Assert.Equal(data.Length, span.Length);
        Assert.True(span.SequenceEqual(data));

        owner.Dispose();
    }

    #endregion

    #region BufferFactory Integration Tests

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetSpanLease_WithPooledBufferWriter_SingleSegment_Works(bool isSensitive)
    {
        using var writer = BufferFactory.CreatePooledBufferWriter<byte>(isSensitive);

        var testData = "Hello, World!"u8.ToArray();
        var span = writer.GetSpan(testData.Length);
        testData.CopyTo(span);
        writer.Advance(testData.Length);

        using var lease = writer.GetSpanLease(isSensitive);

        Assert.Equal(testData.Length, lease.Span.Length);
        Assert.True(lease.Span.SequenceEqual(testData));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConsumeAsContiguousSpan_WithPooledBufferWriter_SingleSegment_Works(bool isSensitive)
    {
        var writer = BufferFactory.CreatePooledBufferWriter<byte>(isSensitive);

        var testData = "Hello, World!"u8.ToArray();
        var span = writer.GetSpan(testData.Length);
        testData.CopyTo(span);
        writer.Advance(testData.Length);

        var owner = writer.ConsumeAsContiguousSpan(isSensitive, out var resultSpan);

        Assert.Equal(testData.Length, resultSpan.Length);
        Assert.True(resultSpan.SequenceEqual(testData));

        owner.Dispose();
    }

    [Fact]
    public void GetSpanLease_WithPooledBufferWriter_MultipleWrites_AccumulatesData()
    {
        using var writer = BufferFactory.CreatePooledBufferWriter<byte>(isSensitive: true);

        var data1 = "First"u8.ToArray();
        var span1 = writer.GetSpan(data1.Length);
        data1.CopyTo(span1);
        writer.Advance(data1.Length);

        var data2 = "Second"u8.ToArray();
        var span2 = writer.GetSpan(data2.Length);
        data2.CopyTo(span2);
        writer.Advance(data2.Length);

        using var lease = writer.GetSpanLease(isSensitive: true);

        Assert.Equal(data1.Length + data2.Length, lease.Span.Length);
        Assert.True(lease.Span[..data1.Length].SequenceEqual(data1));
        Assert.True(lease.Span[data1.Length..].SequenceEqual(data2));
    }

    #endregion
}
