using Ironman.Desk;
using Ironman.SeedData;
using Xunit;

namespace Ironman.Desk.Tests;

public class JsonStoreTests : IDisposable
{
    // 每一條測試自己一個暫存資料夾，跑完刪掉，不碰 repo 裡的任何檔案。
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "Ironman.JsonStore", Guid.NewGuid().ToString("N"));

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void 存完再讀回來四份資料筆數都一樣()
    {
        var data = SeedDataGenerator.Generate(Scale.S);
        var store = new JsonStore(_dir);
        store.SaveAll(RentalDesk.FromSeed(data));

        var desk = store.Load();

        Assert.Equal(data.Books.Count, desk.BookCount);
        Assert.Equal(data.Copies.Count, desk.CopyCount);
        Assert.Equal(data.Members.Count, desk.MemberCount);
        Assert.Equal(data.Loans.Count, desk.LoanCount);
        Assert.Equal(data.Loans.Count(l => !l.IsReturned), desk.OutstandingCount);
    }

    [Fact]
    public void 讀回來的櫃檯索引也對得上()
    {
        // 索引是建構式建的，不是存進檔案的。讀回來之後那三張表必須和借閱清單一致，
        // 否則就是第六天那條「多一份資料就多一條會不會說謊」在檔案這一層重演。
        var data = SeedDataGenerator.Generate(Scale.S);
        var store = new JsonStore(_dir);
        store.SaveAll(RentalDesk.FromSeed(data));

        var desk = store.Load();
        var expected = data.Loans.Where(l => !l.IsReturned).ToList();

        Assert.Equal(expected.Count, desk.AllOutstanding().Count);
        Assert.All(expected, l => Assert.False(desk.IsAvailable(l.CopyId)));
        Assert.Equal(expected.Count, data.Members.Sum(m => desk.OutstandingLoansOf(m.MemberId).Count));
    }

    [Fact]
    public void 中文書名直接存成中文不是轉義碼()
    {
        var data = SeedDataGenerator.Generate(Scale.S);
        var store = new JsonStore(_dir);
        store.SaveAll(RentalDesk.FromSeed(data));

        var text = File.ReadAllText(store.BooksPath);

        Assert.Contains(data.Books[0].Title, text);
        Assert.DoesNotContain("\\u", text);
    }

    [Fact]
    public void 借出之後只重寫借閱檔名冊那三個檔不會被動到()
    {
        var data = SeedDataGenerator.Generate(Scale.S);
        var store = new JsonStore(_dir);
        var desk = RentalDesk.FromSeed(data);
        store.SaveAll(desk);
        var 名冊時間 = new[] { store.BooksPath, store.CopiesPath, store.MembersPath }
            .Select(File.GetLastWriteTimeUtc).ToArray();
        var 借閱時間 = File.GetLastWriteTimeUtc(store.LoansPath);

        Thread.Sleep(20); // 檔案時間的解析度沒那麼細，等一下才看得出差別。
        var 可借 = data.Copies.First(c => desk.IsAvailable(c.CopyId));
        desk.Lend(data.Members[0].MemberId, 可借.CopyId, new DateOnly(2026, 1, 5));
        store.SaveLoans(desk);

        Assert.True(File.GetLastWriteTimeUtc(store.LoansPath) > 借閱時間);
        Assert.Equal(名冊時間, new[] { store.BooksPath, store.CopiesPath, store.MembersPath }
            .Select(File.GetLastWriteTimeUtc).ToArray());
        Assert.Equal(data.Loans.Count + 1, store.Load().LoanCount);
    }

    [Fact]
    public void 少一個檔就當成沒有資料()
    {
        var store = new JsonStore(_dir);
        store.SaveAll(RentalDesk.FromSeed(SeedDataGenerator.Generate(Scale.S)));
        Assert.True(store.Exists());

        File.Delete(store.MembersPath);

        Assert.False(store.Exists());
    }
}
