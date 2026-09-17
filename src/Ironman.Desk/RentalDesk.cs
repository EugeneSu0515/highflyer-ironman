using Ironman.SeedData;

namespace Ironman.Desk;

/// <summary>
/// 櫃檯：借出、歸還、查詢。所有營運規則都在這裡，選單只負責問問題和印結果。
/// </summary>
/// <remarks>
/// 第一版的三個決定，之後每一篇都會回來檢視：
/// 1. 資料全部放在記憶體的 List 裡，程式關掉就沒了（#03 的問題）。
/// 2. 每一次查詢都是從頭掃到尾的線性搜尋（#02 的問題）。
/// 3. 每種書只有一本，所以「這本書借出去了沒」用 ISBN 就能判斷（#02 會被兩本同樣的書打破）。
/// </remarks>
public sealed class RentalDesk
{
    private readonly List<Book> _books;
    private readonly List<Member> _members;
    private readonly List<Loan> _loans;

    public RentalDesk(IEnumerable<Book> books, IEnumerable<Member> members, IEnumerable<Loan> loans)
    {
        _books = books.ToList();
        _members = members.ToList();
        _loans = loans.ToList();
    }

    public static RentalDesk FromSeed(SeedDataSet data) => new(data.Books, data.Members, data.Loans);

    public int BookCount => _books.Count;
    public int MemberCount => _members.Count;
    public int LoanCount => _loans.Count;

    // ------------------------------------------------------------ 查詢

    public Book? FindBook(string isbn) => _books.FirstOrDefault(b => b.Isbn == isbn);

    public Member? FindMember(string memberId) => _members.FirstOrDefault(m => m.MemberId == memberId);

    /// <summary>用書名的一部分找書。紙卡時代這件事叫「憑印象翻」。</summary>
    public IReadOnlyList<Book> SearchBooks(string keyword) =>
        _books.Where(b => b.Title.Contains(keyword, StringComparison.Ordinal)).ToList();

    /// <summary>這本書現在在店裡嗎？沒有任何一筆未歸還的借閱就是在。</summary>
    public bool IsAvailable(string isbn) => OutstandingLoanOf(isbn) is null;

    /// <summary>某位會員手上還沒還的書。</summary>
    public IReadOnlyList<Loan> OutstandingLoansOf(string memberId) =>
        _loans.Where(l => l.MemberId == memberId && !l.IsReturned).ToList();

    /// <summary>所有未歸還的借閱，依借出日排序。</summary>
    public IReadOnlyList<Loan> AllOutstanding() =>
        _loans.Where(l => !l.IsReturned).OrderBy(l => l.LoanDate).ToList();

    private Loan? OutstandingLoanOf(string isbn) =>
        _loans.FirstOrDefault(l => l.Isbn == isbn && !l.IsReturned);

    // ------------------------------------------------------------ 借出

    public Loan Lend(string memberId, string isbn, DateOnly today)
    {
        var member = FindMember(memberId)
            ?? throw new RentalException($"找不到會員 {memberId}，先確認會員卡上的編號。");
        var book = FindBook(isbn)
            ?? throw new RentalException($"找不到 ISBN {isbn} 的書，這本書沒有建檔。");

        var outstanding = OutstandingLoanOf(isbn);
        if (outstanding is not null)
        {
            var holder = FindMember(outstanding.MemberId)?.Name ?? outstanding.MemberId;
            throw new RentalException($"《{book.Title}》已經在 {outstanding.LoanDate:yyyy-MM-dd} 借給 {holder}，還沒回來。");
        }

        var loan = new Loan(member.MemberId, book.Isbn, today, ReturnDate: null);
        _loans.Add(loan);
        return loan;
    }

    // ------------------------------------------------------------ 歸還

    public Loan Return(string isbn, DateOnly today)
    {
        var book = FindBook(isbn)
            ?? throw new RentalException($"找不到 ISBN {isbn} 的書，這本書沒有建檔。");

        var outstanding = OutstandingLoanOf(isbn)
            ?? throw new RentalException($"《{book.Title}》目前不在外面，沒有東西可以歸還。");

        if (today < outstanding.LoanDate)
        {
            throw new RentalException($"歸還日 {today:yyyy-MM-dd} 早於借出日 {outstanding.LoanDate:yyyy-MM-dd}，日期打錯了。");
        }

        // Loan 是不可變的 record，所以「歸還」是用一筆填好歸還日的新紀錄，換掉舊的那一筆。
        var returned = outstanding with { ReturnDate = today };
        var index = _loans.IndexOf(outstanding);
        _loans[index] = returned;
        return returned;
    }
}
