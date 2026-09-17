using Xunit;

namespace Ironman.SeedData.Tests;

public class Isbn13Tests
{
    [Theory]
    [InlineData("9780306406157", true)]  // 已知有效的 ISBN-13 範例（Wikipedia）
    [InlineData("9780306406158", false)] // 改掉最後一碼
    [InlineData("978030640615", false)]  // 長度不對
    [InlineData("978030640615X", false)] // 非數字
    public void 檢查碼驗證(string isbn, bool expected)
    {
        Assert.Equal(expected, Isbn13.IsValid(isbn));
    }

    [Fact]
    public void 產生的ISBN都以978開頭且有效()
    {
        var random = new Random(42);
        for (var i = 0; i < 1_000; i++)
        {
            var isbn = Isbn13.Generate(random);
            Assert.StartsWith("978", isbn);
            Assert.True(Isbn13.IsValid(isbn));
        }
    }
}
