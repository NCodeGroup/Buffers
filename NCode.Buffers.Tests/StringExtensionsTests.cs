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

namespace NCode.Buffers.Tests;

public class StringExtensionsTests
{
    #region SplitSegments Tests

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

    [Fact]
    public void SplitSegments_String_StringSeparator_IsCaseSensitive()
    {
        var segments = "aABbAbC".SplitSegments("ab");

        Assert.Single(segments);
    }

    [Fact]
    public void SplitSegments_ReadOnlyMemory_StringSeparator_IsCaseSensitive()
    {
        var segments = "1SEP2sep3".AsMemory().SplitSegments("sep");

        Assert.Equal(2, segments.Count);

        var list = segments.ToList();
        Assert.Equal("1SEP2", list[0].Memory.ToString());
        Assert.Equal("3", list[1].Memory.ToString());
    }

    #endregion
}
