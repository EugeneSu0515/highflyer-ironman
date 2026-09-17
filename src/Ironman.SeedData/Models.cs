namespace Ironman.SeedData;

/// <summary>書目資料。對應紙卡：借閱卡上緣的書名、作者、ISBN。</summary>
public sealed record Book(string Isbn, string Title, string Author);

/// <summary>會員。對應紙卡：會員卡上的會員編號、姓名、電話。</summary>
public sealed record Member(string MemberId, string Name, string Phone);

/// <summary>
/// 一次借出與歸還。對應紙卡：借閱卡上的每一行。
/// <see cref="ReturnDate"/> 為 null 表示尚未歸還。
/// </summary>
/// <remarks>
/// 階段 0～1 只有 Book，沒有館藏副本（BookCopy），所以這裡以 ISBN 指向書目。
/// 「同一本書買了兩本」的問題會在 #02 出現，Loan 也會在那一篇改為指向 BookCopy。
/// </remarks>
public sealed record Loan(string MemberId, string Isbn, DateOnly LoanDate, DateOnly? ReturnDate)
{
    public bool IsReturned => ReturnDate is not null;
}

/// <summary>一組種子資料。三個集合之間的參照（MemberId、Isbn）保證存在。</summary>
public sealed record SeedDataSet(
    Scale Scale,
    int Seed,
    IReadOnlyList<Book> Books,
    IReadOnlyList<Member> Members,
    IReadOnlyList<Loan> Loans);
