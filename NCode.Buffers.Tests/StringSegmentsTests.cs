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

public class StringSegmentsTests
{
    #region Constructor and Property Tests

    [Fact]
    public void DefaultConstructor_CreatesEmptyInstance()
    {
        var segments = new StringSegments();

        Assert.True(segments.IsEmpty);
        Assert.Empty(segments);
        Assert.True(segments.Original.IsEmpty);
    }

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        const int count = 1;
        var original = "original".AsMemory();
        var first = new MemorySegment<char>(original);

        var segments = new StringSegments(original, count, first);

        Assert.False(segments.IsEmpty);
        Assert.Single(segments);
        Assert.Equal(original, segments.Original);
        Assert.Same(first, segments.First);
    }

    [Fact]
    public void IsEmpty_GivenDefault_ReturnsTrue()
    {
        var segments = new StringSegments();

        Assert.True(segments.IsEmpty);
    }

    [Fact]
    public void IsEmpty_GivenInitialized_ReturnsFalse()
    {
        const int count = 1;
        var original = "original".AsMemory();
        var first = new MemorySegment<char>(original);
        var segments = new StringSegments(original, count, first);

        Assert.False(segments.IsEmpty);
    }

    [Fact]
    public void Original_ReturnsOriginalMemory()
    {
        const int count = 1;
        var original = "original".AsMemory();
        var first = new MemorySegment<char>(original);
        var segments = new StringSegments(original, count, first);

        Assert.Equal(original, segments.Original);
    }

    [Fact]
    public void Count_ReturnsSegmentCount()
    {
        const int count = 3;
        var original = "a.b.c".AsMemory();
        var first = new MemorySegment<char>("a".AsMemory());
        var segments = new StringSegments(original, count, first);

        Assert.Equal(count, segments.Count);
    }

    [Fact]
    public void First_ReturnsFirstSegment()
    {
        const int count = 1;
        var original = "original".AsMemory();
        var first = new MemorySegment<char>(original);
        var segments = new StringSegments(original, count, first);

        Assert.Same(first, segments.First);
    }

    [Fact]
    public void First_WhenEmpty_ThrowsInvalidOperationException()
    {
        var segments = new StringSegments();

        var exception = Assert.Throws<InvalidOperationException>(() => segments.First);
        Assert.Equal("No segments found.", exception.Message);
    }

    #endregion

    #region GetEnumerator Tests

    [Fact]
    public void GetEnumerator_WhenEmpty_YieldsNoElements()
    {
        var segments = new StringSegments();

        Assert.Empty(segments);
    }

    [Fact]
    public void GetEnumerator_ReturnsSingleSegment()
    {
        const int count = 1;
        var original = "test".AsMemory();
        var first = new MemorySegment<char>(original);
        var segments = new StringSegments(original, count, first);

        Assert.Equal([first], segments);
    }

    [Fact]
    public void GetEnumerator_ReturnsAllSegmentsInOrder()
    {
        var segments = StringSegments.Split("1.2.3.4", '.');

        var result = segments.Select(segment => segment.Memory.ToString()).ToList();

        Assert.Equal(["1", "2", "3", "4"], result);
    }

    [Fact]
    public void GetEnumerator_NonGeneric_Works()
    {
        var segments = StringSegments.Split("a,b", ',');
        var enumerable = (System.Collections.IEnumerable)segments;

        var count = 0;
        foreach (var _ in enumerable)
        {
            count++;
        }

        Assert.Equal(2, count);
    }

    #endregion

    #region Split with Char Separator Tests

    [Fact]
    public void Split_String_CharSeparator_SingleSegment()
    {
        var segments = StringSegments.Split("noseparator", '.');

        Assert.Equal("noseparator", segments.Original.ToString());
        Assert.Single(segments);
        Assert.Equal("noseparator", segments.First.Memory.ToString());
    }

    [Fact]
    public void Split_String_CharSeparator_MultipleSegments()
    {
        var segments = StringSegments.Split("1.2.3.4", '.');

        Assert.Equal("1.2.3.4", segments.Original.ToString());
        Assert.Equal(4, segments.Count);

        var list = segments.ToList();
        Assert.Equal("1", list[0].Memory.ToString());
        Assert.Equal("2", list[1].Memory.ToString());
        Assert.Equal("3", list[2].Memory.ToString());
        Assert.Equal("4", list[3].Memory.ToString());
    }

    [Fact]
    public void Split_String_CharSeparator_EmptySegments()
    {
        var segments = StringSegments.Split("a..b", '.');

        Assert.Equal(3, segments.Count);

        var list = segments.ToList();
        Assert.Equal("a", list[0].Memory.ToString());
        Assert.Equal("", list[1].Memory.ToString());
        Assert.Equal("b", list[2].Memory.ToString());
    }

    [Fact]
    public void Split_String_CharSeparator_LeadingEmpty()
    {
        var segments = StringSegments.Split(".a.b", '.');

        Assert.Equal(3, segments.Count);

        var list = segments.ToList();
        Assert.Equal("", list[0].Memory.ToString());
        Assert.Equal("a", list[1].Memory.ToString());
        Assert.Equal("b", list[2].Memory.ToString());
    }

    [Fact]
    public void Split_String_CharSeparator_TrailingEmpty()
    {
        var segments = StringSegments.Split("a.b.", '.');

        Assert.Equal(3, segments.Count);

        var list = segments.ToList();
        Assert.Equal("a", list[0].Memory.ToString());
        Assert.Equal("b", list[1].Memory.ToString());
        Assert.Equal("", list[2].Memory.ToString());
    }

    [Fact]
    public void Split_String_CharSeparator_AllEmpty()
    {
        var segments = StringSegments.Split("...", '.');

        Assert.Equal(4, segments.Count);

        var list = segments.ToList();
        Assert.All(list, segment => Assert.Equal("", segment.Memory.ToString()));
    }

    [Fact]
    public void Split_ReadOnlyMemory_CharSeparator()
    {
        var memory = "x,y,z".AsMemory();

        var segments = StringSegments.Split(memory, ',');

        Assert.Equal(3, segments.Count);

        var list = segments.ToList();
        Assert.Equal("x", list[0].Memory.ToString());
        Assert.Equal("y", list[1].Memory.ToString());
        Assert.Equal("z", list[2].Memory.ToString());
    }

    #endregion

    #region Split with String Separator Tests

    [Fact]
    public void Split_String_StringSeparator_SingleSegment()
    {
        var segments = StringSegments.Split("noseparator", "::");

        Assert.Single(segments);
        Assert.Equal("noseparator", segments.First.Memory.ToString());
    }

    [Fact]
    public void Split_String_StringSeparator_MultipleSegments()
    {
        var segments = StringSegments.Split("a::b::c", "::");

        Assert.Equal(3, segments.Count);

        var list = segments.ToList();
        Assert.Equal("a", list[0].Memory.ToString());
        Assert.Equal("b", list[1].Memory.ToString());
        Assert.Equal("c", list[2].Memory.ToString());
    }

    [Fact]
    public void Split_String_StringSeparator_EmptySegments()
    {
        var segments = StringSegments.Split("a::::b", "::");

        Assert.Equal(3, segments.Count);

        var list = segments.ToList();
        Assert.Equal("a", list[0].Memory.ToString());
        Assert.Equal("", list[1].Memory.ToString());
        Assert.Equal("b", list[2].Memory.ToString());
    }

    [Fact]
    public void Split_String_StringSeparator_CaseSensitive()
    {
        var segments = StringSegments.Split("1ab2Ab3aB4ab5", "ab", StringComparison.Ordinal);

        Assert.Equal(3, segments.Count);

        var list = segments.ToList();
        Assert.Equal("1", list[0].Memory.ToString());
        Assert.Equal("2Ab3aB4", list[1].Memory.ToString());
        Assert.Equal("5", list[2].Memory.ToString());
    }

    [Fact]
    public void Split_String_StringSeparator_CaseInsensitive()
    {
        var segments = StringSegments.Split("1ab2Ab3aB4ab5", "ab", StringComparison.OrdinalIgnoreCase);

        Assert.Equal(5, segments.Count);

        var list = segments.ToList();
        Assert.Equal("1", list[0].Memory.ToString());
        Assert.Equal("2", list[1].Memory.ToString());
        Assert.Equal("3", list[2].Memory.ToString());
        Assert.Equal("4", list[3].Memory.ToString());
        Assert.Equal("5", list[4].Memory.ToString());
    }

    [Fact]
    public void Split_ReadOnlyMemory_StringSeparator()
    {
        var memory = "a--b--c".AsMemory();

        var segments = StringSegments.Split(memory, "--");

        Assert.Equal(3, segments.Count);

        var list = segments.ToList();
        Assert.Equal("a", list[0].Memory.ToString());
        Assert.Equal("b", list[1].Memory.ToString());
        Assert.Equal("c", list[2].Memory.ToString());
    }

    [Fact]
    public void Split_ReadOnlyMemory_StringSeparator_WithComparison()
    {
        var memory = "aXYbxyC".AsMemory();

        var segments = StringSegments.Split(memory, "xy", StringComparison.OrdinalIgnoreCase);

        Assert.Equal(3, segments.Count);

        var list = segments.ToList();
        Assert.Equal("a", list[0].Memory.ToString());
        Assert.Equal("b", list[1].Memory.ToString());
        Assert.Equal("C", list[2].Memory.ToString());
    }

    #endregion

    #region Extension Method Tests

    [Fact]
    public void SplitSegments_String_CharSeparator()
    {
        var segments = "a,b,c".SplitSegments(',');

        Assert.Equal(3, segments.Count);

        var list = segments.ToList();
        Assert.Equal("a", list[0].Memory.ToString());
        Assert.Equal("b", list[1].Memory.ToString());
        Assert.Equal("c", list[2].Memory.ToString());
    }

    [Fact]
    public void SplitSegments_String_StringSeparator()
    {
        var segments = "a::b::c".SplitSegments("::");

        Assert.Equal(3, segments.Count);

        var list = segments.ToList();
        Assert.Equal("a", list[0].Memory.ToString());
        Assert.Equal("b", list[1].Memory.ToString());
        Assert.Equal("c", list[2].Memory.ToString());
    }

    [Fact]
    public void SplitSegments_String_StringSeparator_WithComparison()
    {
        var segments = "aABbAbC".SplitSegments("ab", StringComparison.OrdinalIgnoreCase);

        Assert.Equal(3, segments.Count);

        var list = segments.ToList();
        Assert.Equal("a", list[0].Memory.ToString());
        Assert.Equal("b", list[1].Memory.ToString());
        Assert.Equal("C", list[2].Memory.ToString());
    }

    [Fact]
    public void SplitSegments_ReadOnlyMemory_CharSeparator()
    {
        var memory = "x|y|z".AsMemory();

        var segments = memory.SplitSegments('|');

        Assert.Equal(3, segments.Count);

        var list = segments.ToList();
        Assert.Equal("x", list[0].Memory.ToString());
        Assert.Equal("y", list[1].Memory.ToString());
        Assert.Equal("z", list[2].Memory.ToString());
    }

    [Fact]
    public void SplitSegments_ReadOnlyMemory_StringSeparator()
    {
        var memory = "a=>b=>c".AsMemory();

        var segments = memory.SplitSegments("=>");

        Assert.Equal(3, segments.Count);

        var list = segments.ToList();
        Assert.Equal("a", list[0].Memory.ToString());
        Assert.Equal("b", list[1].Memory.ToString());
        Assert.Equal("c", list[2].Memory.ToString());
    }

    [Fact]
    public void SplitSegments_ReadOnlyMemory_StringSeparator_WithComparison()
    {
        var memory = "1SEP2sep3".AsMemory();

        var segments = memory.SplitSegments("sep", StringComparison.OrdinalIgnoreCase);

        Assert.Equal(3, segments.Count);

        var list = segments.ToList();
        Assert.Equal("1", list[0].Memory.ToString());
        Assert.Equal("2", list[1].Memory.ToString());
        Assert.Equal("3", list[2].Memory.ToString());
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Split_EmptyString_ReturnsSingleEmptySegment()
    {
        var segments = StringSegments.Split("", '.');

        Assert.Single(segments);
        Assert.Equal("", segments.First.Memory.ToString());
    }

    [Fact]
    public void Split_SingleCharacterString_NoSeparator()
    {
        var segments = StringSegments.Split("x", '.');

        Assert.Single(segments);
        Assert.Equal("x", segments.First.Memory.ToString());
    }

    [Fact]
    public void Split_SingleCharacterString_IsSeparator()
    {
        var segments = StringSegments.Split(".", '.');

        Assert.Equal(2, segments.Count);

        var list = segments.ToList();
        Assert.Equal("", list[0].Memory.ToString());
        Assert.Equal("", list[1].Memory.ToString());
    }

    [Fact]
    public void Split_LongStringSeparator()
    {
        var segments = StringSegments.Split("a<separator>b<separator>c", "<separator>");

        Assert.Equal(3, segments.Count);

        var list = segments.ToList();
        Assert.Equal("a", list[0].Memory.ToString());
        Assert.Equal("b", list[1].Memory.ToString());
        Assert.Equal("c", list[2].Memory.ToString());
    }

    [Fact]
    public void Split_SeparatorAtStartAndEnd()
    {
        var segments = StringSegments.Split("||a||b||", "||");

        Assert.Equal(4, segments.Count);

        var list = segments.ToList();
        Assert.Equal("", list[0].Memory.ToString());
        Assert.Equal("a", list[1].Memory.ToString());
        Assert.Equal("b", list[2].Memory.ToString());
        Assert.Equal("", list[3].Memory.ToString());
    }

    [Fact]
    public void Split_OriginalPreservesFullString()
    {
        const string original = "hello,world,test";
        var segments = StringSegments.Split(original, ',');

        Assert.Equal(original, segments.Original.ToString());
    }

    #endregion

    #region IReadOnlyCollection Implementation Tests

    [Fact]
    public void ImplementsIReadOnlyCollection()
    {
        var segments = StringSegments.Split("a,b,c", ',');

        Assert.IsAssignableFrom<IReadOnlyCollection<ReadOnlySequenceSegment<char>>>(segments);
    }

    [Fact]
    public void Count_MatchesActualEnumeratedCount()
    {
        var segments = StringSegments.Split("a.b.c.d.e", '.');

        var enumeratedCount = segments.Count();

        Assert.Equal(segments.Count, enumeratedCount);
    }

    #endregion
}
