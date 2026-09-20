namespace Ironman.SeedData;

/// <summary>書目資料。對應紙卡：借閱卡上緣的書名、作者、ISBN。</summary>
public sealed record Book(string Isbn, string Title, string Author);

/// <summary>
/// 館藏副本：店裡的一本實體書。同一個 ISBN 買了兩本，就是兩個 BookCopy。
/// <see cref="CopyId"/> 是貼在封底上的條碼編號，店員借還時認的是它，不是 ISBN。
/// </summary>
/// <remarks>#02 進場。在此之前每種書只有一本，Loan 直接指向 ISBN 就夠了。</remarks>
public sealed record BookCopy(string CopyId, string Isbn);

/// <summary>會員。對應紙卡：會員卡上的會員編號、姓名、電話。</summary>
public sealed record Member(string MemberId, string Name, string Phone);

/// <summary>
/// 一次借出與歸還。對應紙卡：借閱卡上的每一行。
/// <see cref="ReturnDate"/> 為 null 表示尚未歸還。
/// </summary>
/// <remarks>
/// #02 起 Loan 指向 <see cref="BookCopy"/> 而不是 ISBN：借出去的是那一本實體書，不是那一種書。
/// <see cref="LoanId"/> 是每一筆借閱自己的識別，不靠其他欄位的組合是否恰好不重複；
/// 到 #06 進資料庫時它就是主鍵。
/// </remarks>
public sealed record Loan(int LoanId, string MemberId, string CopyId, DateOnly LoanDate, DateOnly? ReturnDate)
{
    public bool IsReturned => ReturnDate is not null;
}

/// <summary>一組種子資料。集合之間的參照（MemberId、Isbn、CopyId）保證存在。</summary>
public sealed record SeedDataSet(
    Scale Scale,
    int Seed,
    IReadOnlyList<Book> Books,
    IReadOnlyList<BookCopy> Copies,
    IReadOnlyList<Member> Members,
    IReadOnlyList<Loan> Loans);
