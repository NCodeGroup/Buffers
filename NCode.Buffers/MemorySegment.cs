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
using JetBrains.Annotations;

namespace NCode.Buffers;

/// <summary>
/// Provides an implementation of <see cref="ReadOnlySequenceSegment{T}"/> using a linked list of <see cref="ReadOnlyMemory{T}"/> nodes.
/// This class enables building a <see cref="ReadOnlySequence{T}"/> from multiple discontiguous memory blocks.
/// </summary>
/// <typeparam name="T">The type of items in the memory segment.</typeparam>
/// <remarks>
/// Use this class to construct a <see cref="ReadOnlySequence{T}"/> by chaining memory segments together.
/// Start by creating a head segment, then append additional segments using the <see cref="Append"/> method.
/// The resulting linked list can be used to create a <see cref="ReadOnlySequence{T}"/> spanning all segments.
/// </remarks>
/// <example>
/// <code>
/// var first = new MemorySegment&lt;byte&gt;(new byte[] { 1, 2, 3 });
/// var last = first.Append(new byte[] { 4, 5, 6 });
/// var sequence = new ReadOnlySequence&lt;byte&gt;(first, 0, last, last.Memory.Length);
/// </code>
/// </example>
[PublicAPI]
public class MemorySegment<T> : ReadOnlySequenceSegment<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MemorySegment{T}"/> class with the specified memory block.
    /// </summary>
    /// <param name="memory">The block of memory for this node.</param>
    /// <remarks>
    /// This constructor creates a standalone segment with a <see cref="ReadOnlySequenceSegment{T}.RunningIndex"/> of zero.
    /// Use this to create the first (head) segment in a sequence chain.
    /// </remarks>
    public MemorySegment(ReadOnlyMemory<T> memory)
    {
        Memory = memory;
    }

    /// <summary>
    /// Appends a block of memory to the end of the current node, creating a new segment linked to this one.
    /// </summary>
    /// <param name="memory">The block of memory to append to the current node.</param>
    /// <returns>The newly created segment that is now linked after the current segment.</returns>
    /// <remarks>
    /// This method creates a new <see cref="MemorySegment{T}"/> with its <see cref="ReadOnlySequenceSegment{T}.RunningIndex"/>
    /// calculated as the sum of this segment's <see cref="ReadOnlySequenceSegment{T}.RunningIndex"/> and
    /// <see cref="ReadOnlySequenceSegment{T}.Memory"/> length. The new segment is set as this segment's
    /// <see cref="ReadOnlySequenceSegment{T}.Next"/> property.
    /// </remarks>
    public MemorySegment<T> Append(ReadOnlyMemory<T> memory)
    {
        var next = new MemorySegment<T>(memory)
        {
            RunningIndex = RunningIndex + Memory.Length
        };

        Next = next;

        return next;
    }
}
