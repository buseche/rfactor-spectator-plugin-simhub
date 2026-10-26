using System;
using System.Collections.Generic;
using Xunit;

namespace User.RfSpectator.Tests
{
    public class ColorResolverTests
    {
        static Dictionary<string, string> Map(params string[] kv)
        {
            var d = new Dictionary<string, string>();
            for (int i = 0; i < kv.Length; i += 2) d[kv[i]] = kv[i + 1];
            return d;
        }

        [Theory]
        [InlineData("#ff8800", "#FF8800")]
        [InlineData("#FF8800", "#FF8800")]
        [InlineData("  #aBcDeF ", "#ABCDEF")]
        public void Normalize_AcceptsSixDigitHexAndUppercases(string input, string expected)
        {
            Assert.Equal(expected, ColorResolver.Normalize(input));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("FF8800")]
        [InlineData("#F80")]
        [InlineData("#GG8800")]
        [InlineData("#FF88001")]
        [InlineData("red")]
        public void Normalize_RejectsInvalid(string input)
        {
            Assert.Null(ColorResolver.Normalize(input));
        }

        [Fact]
        public void Resolve_ClassColorWhenNoCarColor()
        {
            Assert.Equal("#112233", ColorResolver.Resolve(Map("GT3", "#112233"), Map(), "Ferrari", "GT3"));
        }

        [Fact]
        public void Resolve_IsCaseInsensitiveAndNormalizes()
        {
            Assert.Equal("#112233", ColorResolver.Resolve(Map("gt3", "#112233"), Map(), "x", "GT3"));
            Assert.Equal("#AABBCC", ColorResolver.Resolve(Map(), Map("FERRARI 488", "#aabbcc"), "ferrari 488", "GT3"));
        }

        [Fact]
        public void Resolve_CarOverridesClass()
        {
            Assert.Equal("#AABBCC", ColorResolver.Resolve(Map("GT3", "#112233"), Map("Ferrari", "#AABBCC"), "Ferrari", "GT3"));
        }

        [Fact]
        public void Resolve_NoMatchReturnsEmpty()
        {
            Assert.Equal("", ColorResolver.Resolve(Map("GT3", "#112233"), Map("Ferrari", "#AABBCC"), "Porsche", "LMP"));
            Assert.Equal("", ColorResolver.Resolve(null, null, "Porsche", "LMP"));
            Assert.Equal("", ColorResolver.Resolve(Map(), Map(), null, null));
        }

        [Fact]
        public void Resolve_InvalidCarColorFallsBackToClass()
        {
            Assert.Equal("#112233", ColorResolver.Resolve(Map("GT3", "#112233"), Map("Ferrari", "oops"), "Ferrari", "GT3"));
        }

        [Fact]
        public void With_AddsNormalizedCopyWithoutMutatingOriginal()
        {
            var original = Map("A", "#000000");
            var updated = ColorResolver.With(original, "b", "#0a0b0c");
            Assert.False(original.ContainsKey("b"));
            Assert.Equal("#0A0B0C", updated["B"]);
            Assert.Equal("#000000", updated["a"]);
        }

        [Fact]
        public void With_InvalidColorOrKeyIsIgnored()
        {
            var original = Map("A", "#000000");
            Assert.Same(original, ColorResolver.With(original, "b", "nope"));
            Assert.Same(original, ColorResolver.With(original, " ", "#000000"));
        }

        [Fact]
        public void Without_RemovesCaseInsensitivelyOnCopy()
        {
            var original = Map("A", "#000000");
            var updated = ColorResolver.Without(original, "a");
            Assert.Empty(updated);
            Assert.Single(original);
        }

        [Theory]
        [InlineData(0, 20, 1)]
        [InlineData(-5, 20, 1)]
        [InlineData(7, 20, 7)]
        [InlineData(99, 20, 20)]
        public void ClampRows_BoundsToOneAndMax(int rows, int max, int expected)
        {
            Assert.Equal(expected, CarouselMath.ClampRows(rows, max));
        }
    }
}
