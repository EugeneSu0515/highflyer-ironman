using Ironman.SeedData;
using Xunit;

namespace Ironman.Desk.Tests;

public class RentalDeskTests
{
    // 測試不用種子資料，自己造兩種書、三本副本、兩位會員，規則才看得清楚。
    // 書A 買了兩本（A1、A2），書B 一本（B1）——這就是 #02 的情境。
    private static readonly Book 書A = new("9780000000001", "書A", "作者甲");
    private static readonly Book 書B = new("9780000000002", "書B", "作者乙");
    private static readonly BookCopy A1 = new("C00001", 書A.Isbn);
    private static readonly BookCopy A2 = new("C00002", 書A.Isbn);
    private static readonly BookCopy B1 = new("C00003", 書B.Isbn);
    private static readonly Member 小明 = new("M00001", "小明", "0911-000-001");
    private static readonly Member 小華 = new("M00002", "小華", "0911-000-002");
    private static readonly DateOnly 今天 = new(2026, 1, 5);

    private static RentalDesk 空櫃檯() => new([書A, 書B], [A1, A2, B1], [小明, 小華], []);

    // ------------------------------------------------------------ #01 就有的規則，換成副本後仍成立

    [Fact]
    public void 借出後多一筆未歸還紀錄()
    {
        var desk = 空櫃檯();

        var loan = desk.Lend(小明.MemberId, A1.CopyId, 今天);

        Assert.Equal(小明.MemberId, loan.MemberId);
        Assert.Equal(A1.CopyId, loan.CopyId);
        Assert.False(loan.IsReturned);
        Assert.False(desk.IsAvailable(A1.CopyId));
        Assert.Single(desk.OutstandingLoansOf(小明.MemberId));
    }

    [Fact]
    public void 借給不存在的會員會被擋下()
    {
        var desk = 空櫃檯();

        var ex = Assert.Throws<RentalException>(() => desk.Lend("M99999", A1.CopyId, 今天));

        Assert.Contains("找不到會員", ex.Message);
        Assert.True(desk.IsAvailable(A1.CopyId)); // 沒有留下半筆紀錄
    }

    [Fact]
    public void 借不存在的副本會被擋下()
    {
        var desk = 空櫃檯();

        var ex = Assert.Throws<RentalException>(() => desk.Lend(小明.MemberId, "C99999", 今天));

        Assert.Contains("沒有建檔", ex.Message);
    }

    [Fact]
    public void 同一本副本借出中不能再借給別人()
    {
        var desk = 空櫃檯();
        desk.Lend(小明.MemberId, B1.CopyId, 今天);

        var ex = Assert.Throws<RentalException>(() => desk.Lend(小華.MemberId, B1.CopyId, 今天));

        Assert.Contains("借給 小明", ex.Message);
        Assert.Single(desk.OutstandingLoansOf(小明.MemberId));
        Assert.Empty(desk.OutstandingLoansOf(小華.MemberId));
    }

    [Fact]
    public void 歸還後這本副本可以再借()
    {
        var desk = 空櫃檯();
        desk.Lend(小明.MemberId, A1.CopyId, 今天);

        var returned = desk.Return(A1.CopyId, 今天.AddDays(7));
        var again = desk.Lend(小華.MemberId, A1.CopyId, 今天.AddDays(8));

        Assert.Equal(今天.AddDays(7), returned.ReturnDate);
        Assert.Equal(小華.MemberId, again.MemberId);
        Assert.Empty(desk.OutstandingLoansOf(小明.MemberId));
    }

    [Fact]
    public void 歸還沒借出去的副本會被擋下()
    {
        var desk = 空櫃檯();

        var ex = Assert.Throws<RentalException>(() => desk.Return(B1.CopyId, 今天));

        Assert.Contains("不在外面", ex.Message);
    }

    [Fact]
    public void 歸還日早於借出日會被擋下()
    {
        var desk = 空櫃檯();
        desk.Lend(小明.MemberId, A1.CopyId, 今天);

        var ex = Assert.Throws<RentalException>(() => desk.Return(A1.CopyId, 今天.AddDays(-1)));

        Assert.Contains("日期打錯了", ex.Message);
        Assert.False(desk.IsAvailable(A1.CopyId)); // 還是借出中
    }

    [Fact]
    public void 一次借三本是三筆紀錄()
    {
        var desk = 空櫃檯();

        desk.Lend(小明.MemberId, A1.CopyId, 今天);
        desk.Lend(小明.MemberId, A2.CopyId, 今天);
        desk.Lend(小明.MemberId, B1.CopyId, 今天);

        Assert.Equal(3, desk.OutstandingLoansOf(小明.MemberId).Count);
        Assert.Equal(3, desk.LoanCount);
    }

    [Fact]
    public void 搜尋書名用關鍵字()
    {
        var desk = new RentalDesk(
            [new("9780000000001", "深夜的咖啡館", "甲"), new("9780000000002", "海邊的咖啡館", "乙"), new("9780000000003", "冬季約定", "丙")],
            [],
            [小明],
            []);

        var found = desk.SearchBooks("咖啡館");

        Assert.Equal(2, found.Count);
    }

    // ------------------------------------------------------------ #02 新增：副本

    [Fact]
    public void 同一種書的第二本可以借給別人()
    {
        // #01 版做不到的事：《書A》第一本在小明手上，第二本還能借給小華。
        var desk = 空櫃檯();
        desk.Lend(小明.MemberId, A1.CopyId, 今天);

        var loan = desk.Lend(小華.MemberId, A2.CopyId, 今天);

        Assert.Equal(A2.CopyId, loan.CopyId);
        Assert.Equal(2, desk.CopiesOf(書A.Isbn).Count);
        Assert.Empty(desk.AvailableCopiesOf(書A.Isbn));
    }

    [Fact]
    public void 借出中的副本被拒時會提示店裡還有另一本()
    {
        var desk = 空櫃檯();
        desk.Lend(小明.MemberId, A1.CopyId, 今天);

        var ex = Assert.Throws<RentalException>(() => desk.Lend(小華.MemberId, A1.CopyId, 今天));

        Assert.Contains("店裡還有另一本", ex.Message);
        Assert.Contains(A2.CopyId, ex.Message);
    }

    [Fact]
    public void 同一人同一天借同一種書的兩本_歸還其中一本不會弄錯()
    {
        // #01.2 的 IndexOf 靠「沒有兩筆完全相同的 Loan」，這個情境會讓它失效；流水號解決這件事。
        var desk = 空櫃檯();
        var first = desk.Lend(小明.MemberId, A1.CopyId, 今天);
        var second = desk.Lend(小明.MemberId, A2.CopyId, 今天);

        var returned = desk.Return(A2.CopyId, 今天.AddDays(3));

        Assert.NotEqual(first.LoanId, second.LoanId);
        Assert.Equal(second.LoanId, returned.LoanId);
        Assert.False(desk.IsAvailable(A1.CopyId));
        Assert.True(desk.IsAvailable(A2.CopyId));
        Assert.Single(desk.OutstandingLoansOf(小明.MemberId));
    }

    [Fact]
    public void 借閱流水號接在種子資料之後遞增()
    {
        var data = SeedDataGenerator.Generate(Scale.S);
        var desk = RentalDesk.FromSeed(data);
        var copy = data.Copies.First(c => desk.IsAvailable(c.CopyId));

        var loan = desk.Lend(data.Members[0].MemberId, copy.CopyId, new DateOnly(2026, 1, 5));

        Assert.Equal(data.Loans.Max(l => l.LoanId) + 1, loan.LoanId);
    }

    // ------------------------------------------------------------ #02 新增：索引與資料一致

    [Fact]
    public void 載入種子資料後兩張索引和借閱清單一致()
    {
        var data = SeedDataGenerator.Generate(Scale.S);
        var desk = RentalDesk.FromSeed(data);

        var expected = data.Loans.Where(l => !l.IsReturned).ToList();

        Assert.Equal(expected.Count, desk.OutstandingCount);
        Assert.Equal(expected.Count, desk.AllOutstanding().Count);
        Assert.Equal(expected.Count, data.Members.Sum(m => desk.OutstandingLoansOf(m.MemberId).Count));
        Assert.All(expected, l => Assert.False(desk.IsAvailable(l.CopyId)));
    }

    [Fact]
    public void 借出與歸還之後索引仍和借閱清單一致()
    {
        var desk = 空櫃檯();
        desk.Lend(小明.MemberId, A1.CopyId, 今天);
        desk.Lend(小華.MemberId, A2.CopyId, 今天);
        desk.Lend(小華.MemberId, B1.CopyId, 今天);
        desk.Return(A2.CopyId, 今天.AddDays(1));

        Assert.Equal(2, desk.OutstandingCount);
        Assert.Equal(desk.AllOutstanding().Select(l => l.CopyId).Order(), new[] { A1.CopyId, B1.CopyId }.Order());
        Assert.Single(desk.OutstandingLoansOf(小明.MemberId));
        Assert.Single(desk.OutstandingLoansOf(小華.MemberId));
        Assert.Single(desk.AvailableCopiesOf(書A.Isbn));
    }
}
