using Ironman.SeedData;

namespace Ironman.Bench;

/// <summary>
/// 第三天到第五天的做法搬到副本上：四個 List，每一次查詢都從頭掃到尾。
/// 合法路徑上擋的規則和 RentalDesk 一樣，只差 Return 少一道「歸還日早於借出日」的日期比較，
/// 那一道在量測走的路徑上不會被觸發——比的是「找」的成本，不是功能差異。
/// </summary>
public sealed class LinearDesk
{
    private readonly List<Book> _books;
    private readonly List<BookCopy> _copies;
    private readonly List<Member> _members;
    private readonly List<Loan> _loans;
    private int _nextLoanId;

    public LinearDesk(SeedDataSet data)
    {
        _books = data.Books.ToList();
        _copies = data.Copies.ToList();
        _members = data.Members.ToList();
        _loans = data.Loans.ToList();
        _nextLoanId = _loans.Count == 0 ? 1 : _loans.Max(l => l.LoanId) + 1;
    }

    /// <summary>第三天問的那一個：一百本書掃起來很快，兩千本呢。</summary>
    public Book? FindBook(string isbn) => _books.FirstOrDefault(b => b.Isbn == isbn);

    public bool IsAvailable(string copyId) => OutstandingLoanOf(copyId) is null;

    public IReadOnlyList<Loan> OutstandingLoansOf(string memberId) =>
        _loans.Where(l => l.MemberId == memberId && !l.IsReturned).ToList();

    public Loan Lend(string memberId, string copyId, DateOnly today)
    {
        _ = _members.FirstOrDefault(m => m.MemberId == memberId)
            ?? throw new InvalidOperationException($"找不到會員 {memberId}。");
        _ = _copies.FirstOrDefault(c => c.CopyId == copyId)
            ?? throw new InvalidOperationException($"找不到條碼 {copyId} 的書。");
        if (OutstandingLoanOf(copyId) is not null)
        {
            throw new InvalidOperationException($"條碼 {copyId} 還沒回來。");
        }

        var loan = new Loan(_nextLoanId++, memberId, copyId, today, ReturnDate: null);
        _loans.Add(loan);
        return loan;
    }

    public Loan Return(string copyId, DateOnly today)
    {
        var outstanding = OutstandingLoanOf(copyId)
            ?? throw new InvalidOperationException($"條碼 {copyId} 不在外面。");
        var returned = outstanding with { ReturnDate = today };
        _loans[_loans.FindIndex(l => l.LoanId == outstanding.LoanId)] = returned;
        return returned;
    }

    private Loan? OutstandingLoanOf(string copyId) =>
        _loans.FirstOrDefault(l => l.CopyId == copyId && !l.IsReturned);
}
