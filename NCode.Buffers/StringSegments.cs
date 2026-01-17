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
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Buffers;

/// <summary>
/// Provides the ability to split a string into substrings based on a delimiter without any additional heap allocations.
/// </summary>
/// <remarks>
/// <para>
/// This struct implements <see cref="IReadOnlyCollection{T}"/> of <see cref="ReadOnlySequenceSegment{T}">ReadOnlySequenceSegment&lt;char&gt;</see>,
/// allowing enumeration over the resulting substrings. Each segment represents a portion of the original string
/// between delimiters.
/// </para>
/// <para>
/// The struct is designed for high-performance scenarios where minimizing heap allocations is critical.
/// Use the static <see cref="Split(string, char)"/> or <see cref="Split(ReadOnlyMemory{char}, ReadOnlySpan{char}, StringComparison)"/>
/// methods to create instances.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var segments = StringSegments.Split("one,two,three", ',');
/// foreach (var segment in segments)
/// {
///     Console.WriteLine(segment.Memory.ToString());
/// }
/// </code>
/// </example>
[PublicAPI]
public readonly struct StringSegments : IReadOnlyCollection<ReadOnlySequenceSegment<char>>
{
    /// <summary>
    /// Gets the first segment in the linked list, or <see langword="null"/> if the instance is empty.
    /// </summary>
    private ReadOnlySequenceSegment<char>? FirstOrDefault { get; }

    /// <summary>
    /// Gets a value indicating whether the current instance is empty (contains no segments).
    /// </summary>
    /// <value>
    /// <see langword="true"/> if the instance contains no segments; otherwise, <see langword="false"/>.
    /// </value>
    [MemberNotNullWhen(false, nameof(FirstOrDefault))]
    public bool IsEmpty => FirstOrDefault is null;

    /// <summary>
    /// Gets the original string value that was split.
    /// </summary>
    /// <value>
    /// A <see cref="ReadOnlyMemory{T}">ReadOnlyMemory&lt;char&gt;</see> containing the original unsplit string.
    /// </value>
    public ReadOnlyMemory<char> Original { get; }

    /// <summary>
    /// Gets the number of substrings resulting from the split operation.
    /// </summary>
    /// <value>
    /// The total count of segments. This value is always at least 1 for non-empty instances.
    /// </value>
    public int Count { get; }

    /// <summary>
    /// Gets the first substring segment in the collection.
    /// </summary>
    /// <value>
    /// The first <see cref="ReadOnlySequenceSegment{T}">ReadOnlySequenceSegment&lt;char&gt;</see> in the linked list of segments.
    /// </value>
    /// <exception cref="InvalidOperationException">Thrown when the current instance is empty.</exception>
    public ReadOnlySequenceSegment<char> First =>
        FirstOrDefault ??
        throw new InvalidOperationException("No segments found.");

    /// <summary>
    /// Initializes a new instance of the <see cref="StringSegments"/> struct with the specified values.
    /// </summary>
    /// <param name="original">The original string value that was split.</param>
    /// <param name="count">The number of segments resulting from the split operation.</param>
    /// <param name="first">The first segment in the linked list of substrings.</param>
    /// <remarks>
    /// This constructor is typically not called directly. Use the static <see cref="Split(string, char)"/>
    /// or <see cref="Split(ReadOnlyMemory{char}, ReadOnlySpan{char}, StringComparison)"/> methods instead.
    /// </remarks>
    public StringSegments(ReadOnlyMemory<char> original, int count, ReadOnlySequenceSegment<char> first)
    {
        Original = original;
        Count = count;
        FirstOrDefault = first;
    }

    /// <inheritdoc/>
    public IEnumerator<ReadOnlySequenceSegment<char>> GetEnumerator()
    {
        if (IsEmpty)
        {
            yield break;
        }

        for (var iter = FirstOrDefault; iter != null; iter = iter.Next)
        {
            yield return iter;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Splits a string into substrings based on a single character delimiter without any additional heap allocations.
    /// </summary>
    /// <param name="value">The string to split into substrings.</param>
    /// <param name="separator">A character that delimits the substrings in the original string.</param>
    /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the string that are delimited
    /// by the separator.</returns>
    /// <remarks>
    /// This method uses ordinal comparison for the separator character.
    /// Empty segments are included in the result when consecutive separators are encountered.
    /// </remarks>
    public static StringSegments Split(string value, char separator)
        => Split(value.AsMemory(), separator);

    /// <summary>
    /// Splits a <see cref="ReadOnlyMemory{T}">ReadOnlyMemory&lt;char&gt;</see> into substrings based on a single character delimiter
    /// without any additional heap allocations.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlyMemory{T}">ReadOnlyMemory&lt;char&gt;</see> to split into substrings.</param>
    /// <param name="separator">A character that delimits the substrings in the memory.</param>
    /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the memory that are delimited
    /// by the separator.</returns>
    /// <remarks>
    /// This method uses ordinal comparison for the separator character.
    /// Empty segments are included in the result when consecutive separators are encountered.
    /// </remarks>
    public static StringSegments Split(ReadOnlyMemory<char> value, char separator)
    {
        var count = 1;
        var index = value.Span.IndexOf(separator);
        if (index == -1)
        {
            return new StringSegments(value, count, new MemorySegment<char>(value));
        }

        var first = new MemorySegment<char>(value[..index]);
        var last = first;
        var offset = index + 1;

        while (true)
        {
            ++count;

            index = value.Span[offset..].IndexOf(separator);
            if (index == -1)
            {
                last.Append(value[offset..]);
                return new StringSegments(value, count, first);
            }

            index += offset;
            last = last.Append(value.Slice(offset, index - offset));
            offset = index + 1;
        }
    }

    /// <summary>
    /// Splits a string into substrings based on a string delimiter without any additional heap allocations.
    /// </summary>
    /// <param name="value">The string to split into substrings.</param>
    /// <param name="separator">The string that delimits the substrings in the original string.</param>
    /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the string that are delimited
    /// by the separator.</returns>
    /// <remarks>
    /// This method uses <see cref="StringComparison.Ordinal"/> comparison for the separator string.
    /// Empty segments are included in the result when consecutive separators are encountered.
    /// </remarks>
    public static StringSegments Split(string value, ReadOnlySpan<char> separator)
        => Split(value.AsMemory(), separator, StringComparison.Ordinal);

    /// <summary>
    /// Splits a string into substrings based on a string delimiter without any additional heap allocations,
    /// using the specified comparison rules.
    /// </summary>
    /// <param name="value">The string to split into substrings.</param>
    /// <param name="separator">The string that delimits the substrings in the original string.</param>
    /// <param name="comparisonType">An enumeration value that specifies the rules for the substring search.</param>
    /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the string that are delimited
    /// by the separator.</returns>
    /// <remarks>
    /// Use this overload when you need case-insensitive or culture-aware separator matching.
    /// Empty segments are included in the result when consecutive separators are encountered.
    /// </remarks>
    public static StringSegments Split(string value, ReadOnlySpan<char> separator, StringComparison comparisonType)
        => Split(value.AsMemory(), separator, comparisonType);


    /// <summary>
    /// Splits a <see cref="ReadOnlyMemory{T}">ReadOnlyMemory&lt;char&gt;</see> into substrings based on a string delimiter
    /// without any additional heap allocations.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlyMemory{T}">ReadOnlyMemory&lt;char&gt;</see> to split into substrings.</param>
    /// <param name="separator">The string that delimits the substrings in the memory.</param>
    /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the memory that are delimited
    /// by the separator.</returns>
    /// <remarks>
    /// This method uses <see cref="StringComparison.Ordinal"/> comparison for the separator string.
    /// Empty segments are included in the result when consecutive separators are encountered.
    /// </remarks>
    public static StringSegments Split(ReadOnlyMemory<char> value, ReadOnlySpan<char> separator) =>
        Split(value, separator, StringComparison.Ordinal);

    /// <summary>
    /// Splits a <see cref="ReadOnlyMemory{T}">ReadOnlyMemory&lt;char&gt;</see> into substrings based on a string delimiter
    /// without any additional heap allocations, using the specified comparison rules.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlyMemory{T}">ReadOnlyMemory&lt;char&gt;</see> to split into substrings.</param>
    /// <param name="separator">The string that delimits the substrings in the memory.</param>
    /// <param name="comparisonType">An enumeration value that specifies the rules for the substring search.</param>
    /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the memory that are delimited
    /// by the separator.</returns>
    /// <remarks>
    /// Use this overload when you need case-insensitive or culture-aware separator matching.
    /// Empty segments are included in the result when consecutive separators are encountered.
    /// </remarks>
    public static StringSegments Split(
        ReadOnlyMemory<char> value,
        ReadOnlySpan<char> separator,
        StringComparison comparisonType
    )
    {
        var count = 1;
        var index = value.Span.IndexOf(separator, comparisonType);
        if (index == -1)
        {
            return new StringSegments(value, count, new MemorySegment<char>(value));
        }

        var remaining = value[(index + separator.Length)..];
        var first = new MemorySegment<char>(value[..index]);
        var last = first;

        while (true)
        {
            ++count;

            index = remaining.Span.IndexOf(separator, comparisonType);
            if (index == -1)
            {
                last.Append(remaining);
                return new StringSegments(value, count, first);
            }

            last = last.Append(remaining[..index]);
            remaining = remaining[(index + separator.Length)..];
        }
    }
}
