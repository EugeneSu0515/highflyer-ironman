using Ironman.SeedData;

// 用法：
//   dotnet run --project tools/Ironman.SeedData.Cli -- summary S
//   dotnet run --project tools/Ironman.SeedData.Cli -- cards S > cards.txt
//   dotnet run --project tools/Ironman.SeedData.Cli -- csv S books > books.csv   （books｜members｜loans）
//
// 這個工具只寫到標準輸出。要存成檔案請用 shell 的重導向；
// 檔案 I/O 是 #03 的主題，這裡刻意不碰。

var command = args.Length > 0 ? args[0] : "summary";
var scale = args.Length > 1 ? Enum.Parse<Scale>(args[1], ignoreCase: true) : Scale.S;
var seed = args.Length > 2 && int.TryParse(args[2], out var parsedSeed) ? parsedSeed : SeedDataGenerator.DefaultSeed;

var data = SeedDataGenerator.Generate(scale, seed);

switch (command.ToLowerInvariant())
{
    case "summary":
        PrintSummary(data);
        break;
    case "cards":
        PrintCards(data);
        break;
    case "csv":
        // csv S books 或 csv S 20260905 books：最後一個不是數字的參數就是資料表名稱
        var table = args.Skip(2).LastOrDefault(a => !int.TryParse(a, out _)) ?? "loans";
        if (!PrintCsv(data, table))
        {
            Console.Error.WriteLine($"未知的資料表：{table}（可用：books、members、loans）");
            return 1;
        }
        break;
    default:
        Console.Error.WriteLine($"未知的指令：{command}（可用：summary、cards、csv）");
        return 1;
}

return 0;

static void PrintSummary(SeedDataSet data)
{
    var (start, end) = SeedDataGenerator.PeriodFor(data.Scale);
    var outstanding = data.Loans.Count(l => !l.IsReturned);

    Console.WriteLine($"規模：{data.Scale}    seed：{data.Seed}");
    Console.WriteLine($"期間：{start:yyyy-MM-dd} ～ {end:yyyy-MM-dd}");
    Console.WriteLine($"書籍：{data.Books.Count:N0}");
    Console.WriteLine($"會員：{data.Members.Count:N0}");
    Console.WriteLine($"借閱：{data.Loans.Count:N0}（未歸還 {outstanding:N0}）");
    Console.WriteLine();
    Console.WriteLine("前三本書：");
    foreach (var b in data.Books.Take(3))
    {
        Console.WriteLine($"  {b.Isbn}  {b.Title}／{b.Author}");
    }

    Console.WriteLine("前三位會員：");
    foreach (var m in data.Members.Take(3))
    {
        Console.WriteLine($"  {m.MemberId}  {m.Name}  {m.Phone}");
    }

    Console.WriteLine("前三筆借閱：");
    foreach (var l in data.Loans.Take(3))
    {
        Console.WriteLine($"  {l.LoanDate:yyyy-MM-dd}  {l.MemberId}  {l.Isbn}  {(l.IsReturned ? $"還 {l.ReturnDate:yyyy-MM-dd}" : "未還")}");
    }
}

// 把借閱紀錄印成「紙本出租卡」：每本書一張卡，卡上每一行是一次借還。
// 這就是 #00 問題重現要列印的東西——依書名排列的一疊卡片。
static void PrintCards(SeedDataSet data)
{
    var members = data.Members.ToDictionary(m => m.MemberId);
    var loansByIsbn = data.Loans.ToLookup(l => l.Isbn);

    foreach (var book in data.Books.OrderBy(b => b.Title, StringComparer.Ordinal))
    {
        Console.WriteLine(new string('─', 60));
        Console.WriteLine($"{book.Title}　{book.Author}　ISBN {book.Isbn}");
        Console.WriteLine(new string('─', 60));
        Console.WriteLine("借出日期    會員編號  會員      歸還日期");

        foreach (var loan in loansByIsbn[book.Isbn].OrderBy(l => l.LoanDate))
        {
            var member = members[loan.MemberId];
            var returned = loan.IsReturned ? loan.ReturnDate!.Value.ToString("yyyy-MM-dd") : "（未還）";
            Console.WriteLine($"{loan.LoanDate:yyyy-MM-dd}  {loan.MemberId}    {member.Name,-6}  {returned}");
        }

        Console.WriteLine();
    }
}

// 把一張表印成 CSV，給 #01.3 的試算表試作匯入用。
// 欄位裡沒有逗號、引號或換行（書名、姓名都來自固定詞庫），所以不需要跳脫；日期用 ISO 格式，試算表都認得。
static bool PrintCsv(SeedDataSet data, string table)
{
    switch (table.ToLowerInvariant())
    {
        case "books":
            Console.WriteLine("ISBN,書名,作者");
            foreach (var b in data.Books)
            {
                Console.WriteLine($"{b.Isbn},{b.Title},{b.Author}");
            }
            return true;
        case "members":
            Console.WriteLine("會員編號,姓名,電話");
            foreach (var m in data.Members)
            {
                Console.WriteLine($"{m.MemberId},{m.Name},{m.Phone}");
            }
            return true;
        case "loans":
            Console.WriteLine("會員編號,ISBN,借出日期,歸還日期");
            foreach (var l in data.Loans)
            {
                Console.WriteLine($"{l.MemberId},{l.Isbn},{l.LoanDate:yyyy-MM-dd},{(l.IsReturned ? l.ReturnDate!.Value.ToString("yyyy-MM-dd") : "")}");
            }
            return true;
        default:
            return false;
    }
}
