using Ironman.SeedData;
using Xunit;

namespace Ironman.Desk.Tests;

public class RentalDeskTests
{
    // 測試不用種子資料，自己造三本書、兩位會員，規則才看得清楚。
    // ISBN 的檢查碼是算過的：假資料也守自己的規則，之後加上格式驗證才不會連累這些測試。
    private static readonly Book 書A = new("9780000000002", "書A", "作者甲");
    private static readonly Book 書B = new("9780000000019", "書B", "作者乙");
    private static readonly Book 書C = new("9780000000026", "書C", "作者丙");
    private static readonly Member 小明 = new("M00001", "小明", "0911-000-001");
    private static readonly Member 小華 = new("M00002", "小華", "0911-000-002");
    private static readonly DateOnly 今天 = new(2026, 1, 5);

    private static RentalDesk 空櫃檯() => new([書A, 書B, 書C], [小明, 小華], []);

    [Fact]
    public void 借出後多一筆未歸還紀錄()
    {
        var desk = 空櫃檯();

        var loan = desk.Lend(小明.MemberId, 書A.Isbn, 今天);

        Assert.Equal(小明.MemberId, loan.MemberId);
        Assert.Equal(書A.Isbn, loan.Isbn);
        Assert.False(loan.IsReturned);
        Assert.False(desk.IsAvailable(書A.Isbn));
        Assert.Single(desk.OutstandingLoansOf(小明.MemberId));
    }

    [Fact]
    public void 借給不存在的會員會被擋下()
    {
        var desk = 空櫃檯();

        var ex = Assert.Throws<RentalException>(() => desk.Lend("M99999", 書A.Isbn, 今天));

        Assert.Contains("找不到會員", ex.Message);
        Assert.True(desk.IsAvailable(書A.Isbn)); // 沒有留下半筆紀錄
    }

    [Fact]
    public void 借不存在的書會被擋下()
    {
        var desk = 空櫃檯();

        var ex = Assert.Throws<RentalException>(() => desk.Lend(小明.MemberId, "9789999999999", 今天));

        Assert.Contains("沒有建檔", ex.Message);
    }

    [Fact]
    public void 同一本書借出中不能再借給別人()
    {
        // 目前模型最不能破的規則：每種書只有一本，同一時間只能有一筆未歸還。
        var desk = 空櫃檯();
        desk.Lend(小明.MemberId, 書A.Isbn, 今天);

        var ex = Assert.Throws<RentalException>(() => desk.Lend(小華.MemberId, 書A.Isbn, 今天));

        Assert.Contains("借給 小明", ex.Message);
        Assert.Single(desk.OutstandingLoansOf(小明.MemberId));
        Assert.Empty(desk.OutstandingLoansOf(小華.MemberId));
    }

    [Fact]
    public void 歸還後這本書可以再借()
    {
        var desk = 空櫃檯();
        desk.Lend(小明.MemberId, 書A.Isbn, 今天);

        var returned = desk.Return(書A.Isbn, 今天.AddDays(7));
        var again = desk.Lend(小華.MemberId, 書A.Isbn, 今天.AddDays(8));

        Assert.Equal(今天.AddDays(7), returned.ReturnDate);
        Assert.Equal(小華.MemberId, again.MemberId);
        Assert.Empty(desk.OutstandingLoansOf(小明.MemberId));
    }

    [Fact]
    public void 歸還沒借出去的書會被擋下()
    {
        var desk = 空櫃檯();

        var ex = Assert.Throws<RentalException>(() => desk.Return(書B.Isbn, 今天));

        Assert.Contains("不在外面", ex.Message);
    }

    [Fact]
    public void 歸還日早於借出日會被擋下()
    {
        var desk = 空櫃檯();
        desk.Lend(小明.MemberId, 書A.Isbn, 今天);

        var ex = Assert.Throws<RentalException>(() => desk.Return(書A.Isbn, 今天.AddDays(-1)));

        Assert.Contains("日期打錯了", ex.Message);
        Assert.False(desk.IsAvailable(書A.Isbn)); // 還是借出中
    }

    [Fact]
    public void 一次借三本是三筆紀錄()
    {
        var desk = 空櫃檯();

        desk.Lend(小明.MemberId, 書A.Isbn, 今天);
        desk.Lend(小明.MemberId, 書B.Isbn, 今天);
        desk.Lend(小明.MemberId, 書C.Isbn, 今天);

        Assert.Equal(3, desk.OutstandingLoansOf(小明.MemberId).Count);
        Assert.Equal(3, desk.LoanCount);
    }

    [Fact]
    public void 搜尋書名用關鍵字()
    {
        var desk = new RentalDesk(
            [new("9780000000002", "深夜的咖啡館", "甲"), new("9780000000019", "海邊的咖啡館", "乙"), new("9780000000026", "冬季約定", "丙")],
            [小明],
            []);

        var found = desk.SearchBooks("咖啡館");

        Assert.Equal(2, found.Count);
    }

    [Fact]
    public void 載入種子資料後的未歸還數量和產生器一致()
    {
        var data = SeedDataGenerator.Generate(Scale.S);
        var desk = RentalDesk.FromSeed(data);

        var expected = data.Loans.Count(l => !l.IsReturned);

        Assert.Equal(expected, desk.AllOutstanding().Count);
        Assert.Equal(37, expected); // #00.1 summary S 印出來的數字
    }
}
