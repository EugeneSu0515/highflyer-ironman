using Ironman.Desk;
using Ironman.SeedData;

// 櫃檯程式 v2：借還改掃書背條碼（副本），查找改查表。
// 資料仍在記憶體，程式關掉，這段時間借出、歸還的紀錄就全部消失——這是故意留下的問題，#03 處理。

var desk = RentalDesk.FromSeed(SeedDataGenerator.Generate(Scale.S));
var today = DateOnly.FromDateTime(DateTime.Today);

Console.WriteLine("=== 租書店櫃檯 v2 ===");
Console.WriteLine($"今天 {today:yyyy-MM-dd}｜書 {desk.BookCount} 種 {desk.CopyCount} 本｜會員 {desk.MemberCount} 位｜借閱紀錄 {desk.LoanCount} 筆｜未歸還 {desk.OutstandingCount} 筆");

while (true)
{
    Console.WriteLine();
    Console.WriteLine("1) 借出  2) 歸還  3) 查會員未歸還  4) 查書可不可借  5) 搜尋書名  0) 離開");
    Console.Write("> ");
    var choice = Console.ReadLine()?.Trim();

    try
    {
        switch (choice)
        {
            case "1":
                {
                    var memberId = Ask("會員編號");
                    var copyId = Ask("條碼");
                    var loan = desk.Lend(memberId, copyId, today);
                    Console.WriteLine($"借出成功 #{loan.LoanId}：{TitleOf(loan.CopyId)}（{loan.CopyId}） → {desk.FindMember(loan.MemberId)!.Name}（{loan.LoanDate:yyyy-MM-dd}）");
                    break;
                }
            case "2":
                {
                    var copyId = Ask("條碼");
                    var loan = desk.Return(copyId, today);
                    Console.WriteLine($"歸還成功 #{loan.LoanId}：{TitleOf(loan.CopyId)}（{loan.CopyId}），借出 {loan.LoanDate:yyyy-MM-dd}，歸還 {loan.ReturnDate:yyyy-MM-dd}");
                    break;
                }
            case "3":
                {
                    var memberId = Ask("會員編號");
                    var member = desk.FindMember(memberId) ?? throw new RentalException($"找不到會員 {memberId}。");
                    var loans = desk.OutstandingLoansOf(memberId);
                    Console.WriteLine($"{member.Name} 未歸還 {loans.Count} 本：");
                    foreach (var l in loans)
                    {
                        Console.WriteLine($"  {l.LoanDate:yyyy-MM-dd}  {l.CopyId}  {TitleOf(l.CopyId)}");
                    }
                    break;
                }
            case "4":
                {
                    var isbn = Ask("ISBN");
                    var book = desk.FindBook(isbn) ?? throw new RentalException($"找不到 ISBN {isbn} 的書。");
                    var copies = desk.CopiesOf(isbn);
                    Console.WriteLine($"《{book.Title}》共 {copies.Count} 本，在店 {desk.AvailableCopiesOf(isbn).Count} 本：");
                    foreach (var c in copies)
                    {
                        var held = desk.OutstandingLoanOf(c.CopyId);
                        Console.WriteLine(held is null
                            ? $"  {c.CopyId}  在店"
                            : $"  {c.CopyId}  借出中（{held.LoanDate:yyyy-MM-dd} → {desk.FindMember(held.MemberId)!.Name}）");
                    }
                    break;
                }
            case "5":
                {
                    var keyword = Ask("書名關鍵字");
                    var books = desk.SearchBooks(keyword);
                    Console.WriteLine($"找到 {books.Count} 本：");
                    foreach (var b in books)
                    {
                        Console.WriteLine($"  {b.Isbn}  {b.Title}／{b.Author}  在店 {desk.AvailableCopiesOf(b.Isbn).Count}／{desk.CopiesOf(b.Isbn).Count} 本");
                    }
                    break;
                }
            case "0":
            case null:
                Console.WriteLine("再見。");
                return;
            default:
                Console.WriteLine("請輸入 0～5。");
                break;
        }
    }
    catch (RentalException ex)
    {
        Console.WriteLine($"✗ {ex.Message}");
    }
}

static string Ask(string label)
{
    Console.Write($"{label}：");
    return Console.ReadLine()?.Trim() ?? string.Empty;
}

string TitleOf(string copyId) => desk.FindBook(desk.FindCopy(copyId)!.Isbn)!.Title;
