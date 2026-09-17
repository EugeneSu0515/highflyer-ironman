using Ironman.Desk;
using Ironman.SeedData;

// 第一版櫃檯程式：載入 S 規模種子資料到記憶體，然後進入選單迴圈。
// 程式關掉，這段時間借出、歸還的紀錄就全部消失——這是故意留下的問題，#03 處理。

var desk = RentalDesk.FromSeed(SeedDataGenerator.Generate(Scale.S));
var today = DateOnly.FromDateTime(DateTime.Today);

Console.WriteLine("=== 租書店櫃檯 v1 ===");
Console.WriteLine($"今天 {today:yyyy-MM-dd}｜書 {desk.BookCount} 本｜會員 {desk.MemberCount} 位｜借閱紀錄 {desk.LoanCount} 筆｜未歸還 {desk.AllOutstanding().Count} 筆");

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
                    var isbn = Ask("ISBN");
                    var loan = desk.Lend(memberId, isbn, today);
                    Console.WriteLine($"借出成功：{desk.FindBook(loan.Isbn)!.Title} → {desk.FindMember(loan.MemberId)!.Name}（{loan.LoanDate:yyyy-MM-dd}）");
                    break;
                }
            case "2":
                {
                    var isbn = Ask("ISBN");
                    var loan = desk.Return(isbn, today);
                    Console.WriteLine($"歸還成功：{desk.FindBook(loan.Isbn)!.Title}，借出 {loan.LoanDate:yyyy-MM-dd}，歸還 {loan.ReturnDate:yyyy-MM-dd}");
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
                        Console.WriteLine($"  {l.LoanDate:yyyy-MM-dd}  {desk.FindBook(l.Isbn)!.Title}");
                    }
                    break;
                }
            case "4":
                {
                    var isbn = Ask("ISBN");
                    var book = desk.FindBook(isbn) ?? throw new RentalException($"找不到 ISBN {isbn} 的書。");
                    Console.WriteLine(desk.IsAvailable(isbn) ? $"《{book.Title}》在店裡，可以借。" : $"《{book.Title}》借出中。");
                    break;
                }
            case "5":
                {
                    var keyword = Ask("書名關鍵字");
                    var books = desk.SearchBooks(keyword);
                    Console.WriteLine($"找到 {books.Count} 本：");
                    foreach (var b in books)
                    {
                        Console.WriteLine($"  {b.Isbn}  {b.Title}／{b.Author}  {(desk.IsAvailable(b.Isbn) ? "在店" : "借出中")}");
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
