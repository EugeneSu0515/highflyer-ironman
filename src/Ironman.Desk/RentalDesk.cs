using Ironman.SeedData;

namespace Ironman.Desk;

/// <summary>
/// 櫃檯：借出、歸還、查詢。所有營運規則都在這裡，選單只負責問問題和印結果。
/// </summary>
/// <remarks>
/// 第六天的兩個改變：
/// 1. 借還的對象從「書目（ISBN）」變成「副本（CopyId）」——同一種書買了兩本，兩本可以同時在外面。
/// 2. 查找從 List 線性掃描改成 Dictionary 查表：依副本、依會員各一張，借出與歸還時同步維護。
///    所有借閱的歷史仍留在 _loans，索引只放「還沒回來的」。
/// 沒變的：資料還是在記憶體，程式關掉就沒了（第八天處理）。
/// </remarks>
public sealed class RentalDesk
{
    private readonly Dictionary<string, Book> _booksByIsbn;
    private readonly Dictionary<string, BookCopy> _copiesById;
    private readonly Dictionary<string, List<BookCopy>> _copiesByIsbn;
    private readonly Dictionary<string, Member> _membersById;

    private readonly List<Loan> _loans;
    private readonly Dictionary<string, Loan> _outstandingByCopy;              // CopyId → 那一筆未歸還
    private readonly Dictionary<string, List<Loan>> _outstandingByMember;      // MemberId → 手上所有未歸還
    private int _nextLoanId;

    public RentalDesk(IEnumerable<Book> books, IEnumerable<BookCopy> copies, IEnumerable<Member> members, IEnumerable<Loan> loans)
    {
        _booksByIsbn = books.ToDictionary(b => b.Isbn);
        _copiesById = copies.ToDictionary(c => c.CopyId);
        _copiesByIsbn = _copiesById.Values.GroupBy(c => c.Isbn).ToDictionary(g => g.Key, g => g.ToList());
        _membersById = members.ToDictionary(m => m.MemberId);

        _loans = loans.ToList();
        _outstandingByCopy = new Dictionary<string, Loan>();
        _outstandingByMember = new Dictionary<string, List<Loan>>();
        foreach (var loan in _loans.Where(l => !l.IsReturned))
        {
            Index(loan);
        }

        _nextLoanId = _loans.Count == 0 ? 1 : _loans.Max(l => l.LoanId) + 1;
    }

    public static RentalDesk FromSeed(SeedDataSet data) => new(data.Books, data.Copies, data.Members, data.Loans);

    public int BookCount => _booksByIsbn.Count;
    public int CopyCount => _copiesById.Count;
    public int MemberCount => _membersById.Count;
    public int LoanCount => _loans.Count;
    public int OutstandingCount => _outstandingByCopy.Count;

    // ------------------------------------------------------------ 查詢

    public Book? FindBook(string isbn) => _booksByIsbn.GetValueOrDefault(isbn);

    public BookCopy? FindCopy(string copyId) => _copiesById.GetValueOrDefault(copyId);

    public Member? FindMember(string memberId) => _membersById.GetValueOrDefault(memberId);

    /// <summary>用書名的一部分找書。這個查詢還是線性掃描——它沒有可以當 key 的東西。</summary>
    public IReadOnlyList<Book> SearchBooks(string keyword) =>
        _booksByIsbn.Values.Where(b => b.Title.Contains(keyword, StringComparison.Ordinal)).OrderBy(b => b.Isbn, StringComparer.Ordinal).ToList();

    /// <summary>這種書店裡有幾本副本。</summary>
    public IReadOnlyList<BookCopy> CopiesOf(string isbn) =>
        _copiesByIsbn.TryGetValue(isbn, out var list) ? list : [];

    /// <summary>這種書現在有哪幾本在店裡可借。</summary>
    public IReadOnlyList<BookCopy> AvailableCopiesOf(string isbn) =>
        CopiesOf(isbn).Where(c => IsAvailable(c.CopyId)).ToList();

    /// <summary>這本副本現在在店裡嗎？</summary>
    public bool IsAvailable(string copyId) => !_outstandingByCopy.ContainsKey(copyId);

    /// <summary>這本副本現在在誰手上（null 表示在店裡）。</summary>
    public Loan? OutstandingLoanOf(string copyId) => _outstandingByCopy.GetValueOrDefault(copyId);

    /// <summary>某位會員手上還沒還的書。</summary>
    public IReadOnlyList<Loan> OutstandingLoansOf(string memberId) =>
        _outstandingByMember.TryGetValue(memberId, out var list) ? list.OrderBy(l => l.LoanId).ToList() : [];

    /// <summary>所有未歸還的借閱，依借出日排序。</summary>
    public IReadOnlyList<Loan> AllOutstanding() =>
        _outstandingByCopy.Values.OrderBy(l => l.LoanDate).ThenBy(l => l.LoanId).ToList();

    // ------------------------------------------------------------ 借出

    public Loan Lend(string memberId, string copyId, DateOnly today)
    {
        var member = FindMember(memberId)
            ?? throw new RentalException($"找不到會員 {memberId}，先確認會員卡上的編號。");
        var copy = FindCopy(copyId)
            ?? throw new RentalException($"找不到條碼 {copyId} 的書，這本書沒有建檔。");

        if (_outstandingByCopy.TryGetValue(copyId, out var outstanding))
        {
            var title = _booksByIsbn[copy.Isbn].Title;
            var holder = FindMember(outstanding.MemberId)?.Name ?? outstanding.MemberId;
            var others = AvailableCopiesOf(copy.Isbn);
            var hint = others.Count == 0 ? ""
                : others.Count == 1 ? $"店裡還有另一本《{title}》：{others[0].CopyId}。"
                : $"店裡還有 {others.Count} 本《{title}》可借：{string.Join("、", others.Select(c => c.CopyId))}。";
            throw new RentalException($"條碼 {copyId}《{title}》已經在 {outstanding.LoanDate:yyyy-MM-dd} 借給 {holder}，還沒回來。{hint}");
        }

        var loan = new Loan(_nextLoanId++, member.MemberId, copy.CopyId, today, ReturnDate: null);
        _loans.Add(loan);
        Index(loan);
        return loan;
    }

    // ------------------------------------------------------------ 歸還

    public Loan Return(string copyId, DateOnly today)
    {
        var copy = FindCopy(copyId)
            ?? throw new RentalException($"找不到條碼 {copyId} 的書，這本書沒有建檔。");

        if (!_outstandingByCopy.TryGetValue(copyId, out var outstanding))
        {
            throw new RentalException($"條碼 {copyId}《{_booksByIsbn[copy.Isbn].Title}》目前不在外面，沒有東西可以歸還。");
        }

        if (today < outstanding.LoanDate)
        {
            throw new RentalException($"歸還日 {today:yyyy-MM-dd} 早於借出日 {outstanding.LoanDate:yyyy-MM-dd}，日期打錯了。");
        }

        // Loan 是不可變的 record：用一筆填好歸還日的新紀錄換掉舊的。
        // 每一筆借閱有自己的流水號，就用它找位置，不必靠欄位的組合是否恰好不重複。
        var returned = outstanding with { ReturnDate = today };
        var index = _loans.FindIndex(l => l.LoanId == outstanding.LoanId);
        _loans[index] = returned;
        Unindex(outstanding);
        return returned;
    }

    // ------------------------------------------------------------ 索引維護

    // 兩張索引只放未歸還的借閱。借出時加進去，歸還時拿掉——這就是索引的「同步維護成本」，
    // 忘了其中一邊，查詢就會說謊。
    private void Index(Loan loan)
    {
        _outstandingByCopy[loan.CopyId] = loan;
        if (!_outstandingByMember.TryGetValue(loan.MemberId, out var list))
        {
            list = [];
            _outstandingByMember[loan.MemberId] = list;
        }
        list.Add(loan);
    }

    private void Unindex(Loan loan)
    {
        _outstandingByCopy.Remove(loan.CopyId);
        if (_outstandingByMember.TryGetValue(loan.MemberId, out var list))
        {
            list.RemoveAll(l => l.LoanId == loan.LoanId);
            if (list.Count == 0)
            {
                _outstandingByMember.Remove(loan.MemberId);
            }
        }
    }
}
