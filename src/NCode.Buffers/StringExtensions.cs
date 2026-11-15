// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Buffers;

/// <summary>
/// Provides extension methods to split a string into substrings based on a delimiter without copying the substrings.
/// </summary>
/// <remarks>
/// <para>
/// This class provides string splitting using the C# 14 extension member syntax.
/// Unlike <see cref="string.Split(char[])"/>, which allocates a new string array and a new string for each substring,
/// these methods return a <see cref="StringSegments"/> whose segments are slices of the original value.
/// Splitting isn't allocation-free: it allocates one segment object per substring, and enumerating the result with
/// <see langword="foreach"/> allocates an enumerator object.
/// </para>
/// <para>
/// Extension methods are available for both <see cref="string"/> and <see cref="ReadOnlyMemory{T}">ReadOnlyMemory&lt;char&gt;</see> types.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// foreach (var segment in "one,two,three".SplitSegments(','))
/// {
///     Console.WriteLine(segment.Memory.ToString());
/// }
/// </code>
/// </example>
[PublicAPI]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1708:Identifiers should differ by more than case",
    Justification = "Shipped public API; the two extension blocks are compiler-generated."
)]
public static class StringExtensions
{
    /// <param name="value">The string to split into substrings.</param>
    extension(string value)
    {
        /// <summary>
        /// Splits a string into substrings based on a single character delimiter without copying the substrings.
        /// </summary>
        /// <param name="separator">A character that delimits the substrings in the original string.</param>
        /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the string that are delimited
        /// by the separator.</returns>
        /// <remarks>
        /// This method uses ordinal comparison for the separator character.
        /// Empty segments are included in the result when consecutive separators are encountered.
        /// The substrings aren't copied, but the split allocates one segment object per substring.
        /// </remarks>
        public StringSegments SplitSegments(char separator) =>
            StringSegments.Split(value, separator);

        /// <summary>
        /// Splits a string into substrings based on a string delimiter without copying the substrings.
        /// </summary>
        /// <param name="separator">The string that delimits the substrings in the original string.</param>
        /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the string that are delimited
        /// by the separator.</returns>
        /// <remarks>
        /// This method uses <see cref="StringComparison.Ordinal"/> comparison for the separator string.
        /// Empty segments are included in the result when consecutive separators are encountered.
        /// The substrings aren't copied, but the split allocates one segment object per substring.
        /// </remarks>
        public StringSegments SplitSegments(ReadOnlySpan<char> separator) =>
            StringSegments.Split(value, separator);

        /// <summary>
        /// Splits a string into substrings based on a string delimiter without copying the substrings,
        /// using the specified comparison rules.
        /// </summary>
        /// <param name="separator">The string that delimits the substrings in the original string.</param>
        /// <param name="comparisonType">An enumeration value that specifies the rules for the substring search.</param>
        /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the string that are delimited
        /// by the separator.</returns>
        /// <remarks>
        /// Use this overload when you need case-insensitive or culture-aware separator matching.
        /// Empty segments are included in the result when consecutive separators are encountered.
        /// The substrings aren't copied, but the split allocates one segment object per substring.
        /// </remarks>
        public StringSegments SplitSegments(
            ReadOnlySpan<char> separator,
            StringComparison comparisonType
        ) => StringSegments.Split(value, separator, comparisonType);
    }

    /// <param name="value">The <see cref="ReadOnlyMemory{T}">ReadOnlyMemory&lt;char&gt;</see> to split into substrings.</param>
    extension(ReadOnlyMemory<char> value)
    {
        /// <summary>
        /// Splits a <see cref="ReadOnlyMemory{T}">ReadOnlyMemory&lt;char&gt;</see> into substrings based on a single character delimiter
        /// without copying the substrings.
        /// </summary>
        /// <param name="separator">A character that delimits the substrings in the memory.</param>
        /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the memory that are delimited
        /// by the separator.</returns>
        /// <remarks>
        /// This method uses ordinal comparison for the separator character.
        /// Empty segments are included in the result when consecutive separators are encountered.
        /// The substrings aren't copied, but the split allocates one segment object per substring.
        /// </remarks>
        public StringSegments SplitSegments(char separator) =>
            StringSegments.Split(value, separator);

        /// <summary>
        /// Splits a <see cref="ReadOnlyMemory{T}">ReadOnlyMemory&lt;char&gt;</see> into substrings based on a string delimiter
        /// without copying the substrings.
        /// </summary>
        /// <param name="separator">The string that delimits the substrings in the memory.</param>
        /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the memory that are delimited
        /// by the separator.</returns>
        /// <remarks>
        /// This method uses <see cref="StringComparison.Ordinal"/> comparison for the separator string.
        /// Empty segments are included in the result when consecutive separators are encountered.
        /// The substrings aren't copied, but the split allocates one segment object per substring.
        /// </remarks>
        public StringSegments SplitSegments(ReadOnlySpan<char> separator) =>
            StringSegments.Split(value, separator);

        /// <summary>
        /// Splits a <see cref="ReadOnlyMemory{T}">ReadOnlyMemory&lt;char&gt;</see> into substrings based on a string delimiter
        /// without copying the substrings, using the specified comparison rules.
        /// </summary>
        /// <param name="separator">The string that delimits the substrings in the memory.</param>
        /// <param name="comparisonType">An enumeration value that specifies the rules for the substring search.</param>
        /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the memory that are delimited
        /// by the separator.</returns>
        /// <remarks>
        /// Use this overload when you need case-insensitive or culture-aware separator matching.
        /// Empty segments are included in the result when consecutive separators are encountered.
        /// The substrings aren't copied, but the split allocates one segment object per substring.
        /// </remarks>
        public StringSegments SplitSegments(
            ReadOnlySpan<char> separator,
            StringComparison comparisonType
        ) => StringSegments.Split(value, separator, comparisonType);
    }
}
