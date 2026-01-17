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

using JetBrains.Annotations;

namespace NCode.Buffers;

/// <summary>
/// Provides extension methods to split a string into substrings based on a delimiter without any additional heap allocations.
/// </summary>
/// <remarks>
/// <para>
/// This class provides zero-allocation string splitting functionality using the C# 14 extension member syntax.
/// Unlike <see cref="string.Split(char[])"/> which allocates a new string array on each call,
/// these methods return a <see cref="StringSegments"/> struct that can be enumerated without heap allocations.
/// </para>
/// <para>
/// Extension methods are available for both <see cref="string"/> and <see cref="ReadOnlyMemory{T}">ReadOnlyMemory&lt;char&gt;</see> types.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// foreach (var segment in "one,two,three".SplitSegments(','))
/// {
///     Console.WriteLine(segment.ToString());
/// }
/// </code>
/// </example>
[PublicAPI]
public static class StringExtensions
{
    /// <param name="value">The string to split into substrings.</param>
    extension(string value)
    {
        /// <summary>
        /// Splits a string into substrings based on a single character delimiter without any additional heap allocations.
        /// </summary>
        /// <param name="separator">A character that delimits the substrings in the original string.</param>
        /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the string that are delimited
        /// by the separator.</returns>
        /// <remarks>
        /// This method uses ordinal comparison for the separator character.
        /// Empty segments are included in the result when consecutive separators are encountered.
        /// </remarks>
        public StringSegments SplitSegments(char separator) =>
            StringSegments.Split(value, separator);

        /// <summary>
        /// Splits a string into substrings based on a string delimiter without any additional heap allocations.
        /// </summary>
        /// <param name="separator">The string that delimits the substrings in the original string.</param>
        /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the string that are delimited
        /// by the separator.</returns>
        /// <remarks>
        /// This method uses <see cref="StringComparison.Ordinal"/> comparison for the separator string.
        /// Empty segments are included in the result when consecutive separators are encountered.
        /// </remarks>
        public StringSegments SplitSegments(ReadOnlySpan<char> separator) =>
            StringSegments.Split(value, separator);

        /// <summary>
        /// Splits a string into substrings based on a string delimiter without any additional heap allocations,
        /// using the specified comparison rules.
        /// </summary>
        /// <param name="separator">The string that delimits the substrings in the original string.</param>
        /// <param name="comparisonType">An enumeration value that specifies the rules for the substring search.</param>
        /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the string that are delimited
        /// by the separator.</returns>
        /// <remarks>
        /// Use this overload when you need case-insensitive or culture-aware separator matching.
        /// Empty segments are included in the result when consecutive separators are encountered.
        /// </remarks>
        public StringSegments SplitSegments(ReadOnlySpan<char> separator, StringComparison comparisonType) =>
            StringSegments.Split(value, separator, comparisonType);
    }

    /// <param name="value">The <see cref="ReadOnlyMemory{T}">ReadOnlyMemory&lt;char&gt;</see> to split into substrings.</param>
    extension(ReadOnlyMemory<char> value)
    {
        /// <summary>
        /// Splits a <see cref="ReadOnlyMemory{T}">ReadOnlyMemory&lt;char&gt;</see> into substrings based on a single character delimiter
        /// without any additional heap allocations.
        /// </summary>
        /// <param name="separator">A character that delimits the substrings in the memory.</param>
        /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the memory that are delimited
        /// by the separator.</returns>
        /// <remarks>
        /// This method uses ordinal comparison for the separator character.
        /// Empty segments are included in the result when consecutive separators are encountered.
        /// </remarks>
        public StringSegments SplitSegments(char separator) =>
            StringSegments.Split(value, separator);

        /// <summary>
        /// Splits a <see cref="ReadOnlyMemory{T}">ReadOnlyMemory&lt;char&gt;</see> into substrings based on a string delimiter
        /// without any additional heap allocations.
        /// </summary>
        /// <param name="separator">The string that delimits the substrings in the memory.</param>
        /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the memory that are delimited
        /// by the separator.</returns>
        /// <remarks>
        /// This method uses <see cref="StringComparison.Ordinal"/> comparison for the separator string.
        /// Empty segments are included in the result when consecutive separators are encountered.
        /// </remarks>
        public StringSegments SplitSegments(ReadOnlySpan<char> separator) =>
            StringSegments.Split(value, separator);

        /// <summary>
        /// Splits a <see cref="ReadOnlyMemory{T}">ReadOnlyMemory&lt;char&gt;</see> into substrings based on a string delimiter
        /// without any additional heap allocations, using the specified comparison rules.
        /// </summary>
        /// <param name="separator">The string that delimits the substrings in the memory.</param>
        /// <param name="comparisonType">An enumeration value that specifies the rules for the substring search.</param>
        /// <returns>A <see cref="StringSegments"/> instance that contains the substrings from the memory that are delimited
        /// by the separator.</returns>
        /// <remarks>
        /// Use this overload when you need case-insensitive or culture-aware separator matching.
        /// Empty segments are included in the result when consecutive separators are encountered.
        /// </remarks>
        public StringSegments SplitSegments(ReadOnlySpan<char> separator, StringComparison comparisonType) =>
            StringSegments.Split(value, separator, comparisonType);
    }
}
