using Xunit;

namespace Ironman.SeedData.Tests;

public class SeedDataGeneratorTests
{
    [Fact]
    public void 同一個seed產生完全相同的資料()
    {
        var a = SeedDataGenerator.Generate(Scale.S);
        var b = SeedDataGenerator.Generate(Scale.S);

        Assert.Equal(a.Books, b.Books);
        Assert.Equal(a.Members, b.Members);
        Assert.Equal(a.Loans, b.Loans);
    }

    [Fact]
    public void 不同seed產生不同的資料()
    {
        var a = SeedDataGenerator.Generate(Scale.S, seed: 1);
        var b = SeedDataGenerator.Generate(Scale.S, seed: 2);

        // 只比第一本書不夠：那一本偶然相同，整組資料其實不同，測試也會紅。
        Assert.NotEqual(a.Books, b.Books);
        Assert.NotEqual(a.Members, b.Members);
        Assert.NotEqual(a.Loans, b.Loans);
    }

    [Theory]
    [InlineData(Scale.S, 100, 40, 500)]
    [InlineData(Scale.M, 2_000, 600, 50_000)]
    public void 每個規模的數量符合定義(Scale scale, int books, int members, int loans)
    {
        var data = SeedDataGenerator.Generate(scale);

        Assert.Equal(books, data.Books.Count);
        Assert.Equal(members, data.Members.Count);
        Assert.Equal(loans, data.Loans.Count);
    }

    [Fact]
    public void ISBN不重複且檢查碼有效()
    {
        var data = SeedDataGenerator.Generate(Scale.M);

        Assert.Equal(data.Books.Count, data.Books.Select(b => b.Isbn).Distinct().Count());
        Assert.All(data.Books, b => Assert.True(Isbn13.IsValid(b.Isbn), $"{b.Isbn} 檢查碼錯誤"));
    }

    [Fact]
    public void 會員編號不重複()
    {
        var data = SeedDataGenerator.Generate(Scale.S);

        Assert.Equal(data.Members.Count, data.Members.Select(m => m.MemberId).Distinct().Count());
    }

    [Fact]
    public void 每筆借閱都指向存在的書與會員()
    {
        var data = SeedDataGenerator.Generate(Scale.S);
        var isbns = data.Books.Select(b => b.Isbn).ToHashSet();
        var memberIds = data.Members.Select(m => m.MemberId).ToHashSet();

        Assert.All(data.Loans, l =>
        {
            Assert.Contains(l.Isbn, isbns);
            Assert.Contains(l.MemberId, memberIds);
        });
    }

    [Fact]
    public void 歸還日不早於借出日()
    {
        var data = SeedDataGenerator.Generate(Scale.S);

        Assert.All(data.Loans.Where(l => l.IsReturned), l => Assert.True(l.ReturnDate >= l.LoanDate));
    }

    [Fact]
    public void 同一本書的借閱期間不重疊()
    {
        // 階段 0～1 每本書只有一本：借出去就不能再借，直到歸還。
        var data = SeedDataGenerator.Generate(Scale.S);
        var (_, periodEnd) = SeedDataGenerator.PeriodFor(Scale.S);

        foreach (var group in data.Loans.GroupBy(l => l.Isbn))
        {
            var ordered = group.OrderBy(l => l.LoanDate).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var previousEnd = ordered[i - 1].ReturnDate ?? periodEnd;
                Assert.True(ordered[i].LoanDate > previousEnd,
                    $"{group.Key} 第 {i} 筆借出日 {ordered[i].LoanDate} 早於前一筆歸還日 {previousEnd}");
            }
        }
    }

    [Fact]
    public void 借閱日期都落在該規模的期間內()
    {
        var data = SeedDataGenerator.Generate(Scale.S);
        var (start, end) = SeedDataGenerator.PeriodFor(Scale.S);

        Assert.All(data.Loans, l => Assert.InRange(l.LoanDate, start, end));
    }

    [Fact]
    public void 有一部分借閱尚未歸還()
    {
        var data = SeedDataGenerator.Generate(Scale.S);

        Assert.Contains(data.Loans, l => !l.IsReturned);
    }
}
