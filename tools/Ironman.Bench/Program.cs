using System.Diagnostics;
using Ironman.Bench;
using Ironman.Desk;
using Ironman.SeedData;

// 用法：dotnet run -c Release --project tools/Ironman.Bench -- S
//
// 同一組種子資料、同一串查詢，分別餵給 LinearDesk（第三天到第五天的做法）
// 與 RentalDesk（第六天的查表）。每個操作跑 5 輪取中位數，先跑一輪暖機不計入。
// 數字只在同一台機器、同一次執行裡互相比較有意義。換一次執行結果就會變，換一台機器要重新驗證。

var scale = args.Length > 0 ? Enum.Parse<Scale>(args[0], ignoreCase: true) : Scale.S;
const int Ops = 10_000;

var data = SeedDataGenerator.Generate(scale);
var today = new DateOnly(2026, 1, 5);

Console.WriteLine($"規模 {scale}：書 {data.Books.Count:N0} 種 {data.Copies.Count:N0} 本｜會員 {data.Members.Count:N0}｜借閱 {data.Loans.Count:N0}（未歸還 {data.Loans.Count(l => !l.IsReturned):N0}）");
#if DEBUG
const string Configuration = "Debug（請改用 dotnet run -c Release）";
#else
const string Configuration = "Release";
#endif
Console.WriteLine($"三種查詢各 {Ops:N0} 次；借還是每本當下可借的副本各借還一次。5 輪取中位數；.NET {Environment.Version}，{Configuration}");
Console.WriteLine();

// 同一串查詢：固定 seed，兩邊拿到一模一樣的順序。
var rng = new Random(42);
var isbns = Enumerable.Range(0, Ops).Select(_ => data.Books[rng.Next(data.Books.Count)].Isbn).ToArray();
var copyIds = Enumerable.Range(0, Ops).Select(_ => data.Copies[rng.Next(data.Copies.Count)].CopyId).ToArray();
var memberIds = Enumerable.Range(0, Ops).Select(_ => data.Members[rng.Next(data.Members.Count)].MemberId).ToArray();

// 借出＋歸還要挑在店裡的副本，而且同一個副本不能在同一輪借兩次。
var outstanding = data.Loans.Where(l => !l.IsReturned).Select(l => l.CopyId).ToHashSet();
var lendCopies = data.Copies.Where(c => !outstanding.Contains(c.CopyId)).Select(c => c.CopyId).Take(Ops).ToArray();
var lendMembers = lendCopies.Select(_ => data.Members[rng.Next(data.Members.Count)].MemberId).ToArray();

Console.WriteLine($"{"操作",-28}{"線性 List",14}{"Dictionary",14}{"倍數",10}");
Console.WriteLine(new string('─', 66));

// 這一列量的是建立本身：setup 什麼都不做，被計時的就是建櫃檯這件事。
Report("建立櫃檯（載入 + 建索引）",
    () => () => { _ = new LinearDesk(data); },
    () => () => { _ = RentalDesk.FromSeed(data); });

// 第三天結尾問的就是這一列：FindBook 一路掃書目，一百本很快，兩千本呢。
Report($"依ISBN查書 ×{Ops:N0}",
    () => { var d = new LinearDesk(data); return () => { foreach (var isbn in isbns) d.FindBook(isbn); }; },
    () => { var d = RentalDesk.FromSeed(data); return () => { foreach (var isbn in isbns) d.FindBook(isbn); }; });

Report($"查副本在不在店裡 ×{Ops:N0}",
    () => { var d = new LinearDesk(data); return () => { foreach (var id in copyIds) d.IsAvailable(id); }; },
    () => { var d = RentalDesk.FromSeed(data); return () => { foreach (var id in copyIds) d.IsAvailable(id); }; });

Report($"查會員未歸還 ×{Ops:N0}",
    () => { var d = new LinearDesk(data); return () => { foreach (var id in memberIds) d.OutstandingLoansOf(id); }; },
    () => { var d = RentalDesk.FromSeed(data); return () => { foreach (var id in memberIds) d.OutstandingLoansOf(id); }; });

Report($"借出＋歸還 ×{lendCopies.Length:N0}",
    () => { var d = new LinearDesk(data); return () => Cycle(lendMembers, lendCopies, today, d.Lend, d.Return); },
    () => { var d = RentalDesk.FromSeed(data); return () => Cycle(lendMembers, lendCopies, today, d.Lend, d.Return); });

Console.WriteLine();
Console.WriteLine("搜尋書名沒有換做法，兩邊都是線性掃描，所以沒有這一列。");

return;

static void Cycle(string[] members, string[] copies, DateOnly today,
                  Func<string, string, DateOnly, Loan> lend, Func<string, DateOnly, Loan> giveBack)
{
    for (var i = 0; i < copies.Length; i++)
    {
        lend(members[i], copies[i], today);
        giveBack(copies[i], today);
    }
}

// 每一輪重新建一個櫃檯，要量的動作在 setup 回傳的委派裡；建立本身不計時。
static void Report(string label, Func<Action> linearSetup, Func<Action> indexedSetup)
{
    var linear = Median(linearSetup);
    var indexed = Median(indexedSetup);
    var ratio = indexed.TotalMilliseconds > 0 ? linear.TotalMilliseconds / indexed.TotalMilliseconds : double.PositiveInfinity;
    Console.WriteLine($"{label,-28}{Fmt(linear),14}{Fmt(indexed),14}{ratio,9:N1}x");
}

static TimeSpan Median(Func<Action> setup)
{
    const int Rounds = 5;
    setup()(); // 暖機：JIT 編譯這一次不算。
    var samples = new List<TimeSpan>(Rounds);
    for (var r = 0; r < Rounds; r++)
    {
        var action = setup();
        GC.Collect();
        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();
        samples.Add(sw.Elapsed);
    }
    samples.Sort();
    return samples[Rounds / 2];
}

static string Fmt(TimeSpan t) =>
    t.TotalMilliseconds >= 1000 ? $"{t.TotalSeconds:N2} s"
    : t.TotalMilliseconds >= 1 ? $"{t.TotalMilliseconds:N1} ms"
    : $"{t.TotalMilliseconds * 1000:N0} µs";
