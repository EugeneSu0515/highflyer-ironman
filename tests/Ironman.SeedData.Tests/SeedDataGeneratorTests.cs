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
        Assert.Equal(a.Copies, b.Copies);
        Assert.Equal(a.Members, b.Members);
        Assert.Equal(a.Loans, b.Loans);
    }

    [Fact]
    public void 不同seed產生不同的資料()
    {
        var a = SeedDataGenerator.Generate(Scale.S, seed: 1);
        var b = SeedDataGenerator.Generate(Scale.S, seed: 2);

        Assert.NotEqual(a.Books[0], b.Books[0]);
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
    public void 每種書至少一本副本且有些書不只一本()
    {
        var data = SeedDataGenerator.Generate(Scale.S);
        var copiesPerBook = data.Copies.GroupBy(c => c.Isbn).ToDictionary(g => g.Key, g => g.Count());

        Assert.All(data.Books, b => Assert.True(copiesPerBook.GetValueOrDefault(b.Isbn) >= 1, $"{b.Isbn} 沒有副本"));
        Assert.Contains(copiesPerBook.Values, n => n >= 2);
        Assert.Equal(data.Copies.Count, data.Copies.Select(c => c.CopyId).Distinct().Count());
    }

    [Fact]
    public void 副本依借閱次數排名分配_前十五名各兩本其餘各一本()
    {
        // #02 新增的規則：老闆照排行榜進貨，前 15 名各多進一本。
        // 同分時書目順序在後的排前面，和 #01.3 試算表排行榜的排序鍵（列號大的贏）同方向；
        // 方向一旦相反，第 15 名附近二十幾本同分的書就會換一批進貨。
        var data = SeedDataGenerator.Generate(Scale.S);
        var isbnOfCopy = data.Copies.ToDictionary(c => c.CopyId, c => c.Isbn);
        var loanCount = data.Books.ToDictionary(b => b.Isbn, _ => 0);
        foreach (var loan in data.Loans)
        {
            loanCount[isbnOfCopy[loan.CopyId]]++;
        }

        var copiesPerBook = data.Copies.GroupBy(c => c.Isbn).ToDictionary(g => g.Key, g => g.Count());
        var ranked = Enumerable.Range(0, data.Books.Count)
            .OrderByDescending(i => loanCount[data.Books[i].Isbn])
            .ThenByDescending(i => i)
            .ToArray();

        for (var rank = 0; rank < ranked.Length; rank++)
        {
            var isbn = data.Books[ranked[rank]].Isbn;
            Assert.Equal(rank < 15 ? 2 : 1, copiesPerBook[isbn]);
        }

        Assert.Equal(data.Books.Count + 15, data.Copies.Count);
    }

    [Fact]
    public void 每筆借閱都指向存在的副本與會員()
    {
        var data = SeedDataGenerator.Generate(Scale.S);
        var copyIds = data.Copies.Select(c => c.CopyId).ToHashSet();
        var memberIds = data.Members.Select(m => m.MemberId).ToHashSet();

        Assert.All(data.Loans, l =>
        {
            Assert.Contains(l.CopyId, copyIds);
            Assert.Contains(l.MemberId, memberIds);
        });
    }

    [Fact]
    public void 借閱流水號從1起連號不重複()
    {
        var data = SeedDataGenerator.Generate(Scale.S);

        Assert.Equal(Enumerable.Range(1, data.Loans.Count), data.Loans.Select(l => l.LoanId));
    }

    [Fact]
    public void 歸還日不早於借出日()
    {
        var data = SeedDataGenerator.Generate(Scale.S);

        Assert.All(data.Loans.Where(l => l.IsReturned), l => Assert.True(l.ReturnDate >= l.LoanDate));
    }

    [Fact]
    public void 同一本副本的借閱期間不重疊()
    {
        // 一本實體書借出去就不能再借，直到歸還。同一種書的另一本副本則不受影響。
        var data = SeedDataGenerator.Generate(Scale.S);
        var (_, periodEnd) = SeedDataGenerator.PeriodFor(Scale.S);

        foreach (var group in data.Loans.GroupBy(l => l.CopyId))
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
