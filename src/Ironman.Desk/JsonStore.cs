using System.Text.Encodings.Web;
using System.Text.Json;
using Ironman.SeedData;

namespace Ironman.Desk;

/// <summary>店裡的資料夾現在是什麼狀態。</summary>
public enum StoreState
{
    /// <summary>四個檔一個都沒有：第一次開店。</summary>
    Empty,

    /// <summary>四個檔都在：正常載入。</summary>
    Complete,

    /// <summary>有幾個在、有幾個不在。這是事故，不是第一次開店。</summary>
    Incomplete,
}

/// <summary>
/// 把櫃檯的四份資料存成四個 JSON 檔，再讀回來。第八天進場：關機之後借還紀錄要留著。
/// </summary>
/// <remarks>
/// 一個實體一個檔：名冊很少變、借閱天天變，分開存才不用每次都重寫名冊。
/// 這一版是最單純的寫法——直接對正本寫。第九天會看到它寫到一半斷電的樣子。
/// </remarks>
public sealed class JsonStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 書名、人名直接存中文，不存成 \uXXXX
    };

    public string Directory { get; }

    public JsonStore(string directory) => Directory = directory;

    public string BooksPath => Path.Combine(Directory, "books.json");
    public string CopiesPath => Path.Combine(Directory, "copies.json");
    public string MembersPath => Path.Combine(Directory, "members.json");
    public string LoansPath => Path.Combine(Directory, "loans.json");

    private string[] AllPaths => [BooksPath, CopiesPath, MembersPath, LoansPath];

    /// <summary>
    /// 三種狀態，不是兩種。「少一個檔」和「一個都沒有」必須分開——
    /// 把前者當成後者，就會拿種子資料蓋掉那幾個還好好的檔。
    /// </summary>
    public StoreState State()
    {
        var 在的 = AllPaths.Count(File.Exists);
        if (在的 == 0)
        {
            return StoreState.Empty;
        }

        return 在的 == AllPaths.Length ? StoreState.Complete : StoreState.Incomplete;
    }

    /// <summary>哪幾個檔不見了。給呼叫端印出來，讓人知道要去找什麼。</summary>
    public IReadOnlyList<string> MissingFiles() =>
        AllPaths.Where(p => !File.Exists(p)).Select(Path.GetFileName).ToList()!;

    public void SaveAll(RentalDesk desk)
    {
        System.IO.Directory.CreateDirectory(Directory);
        Write(BooksPath, desk.Books);
        Write(CopiesPath, desk.Copies);
        Write(MembersPath, desk.Members);
        Write(LoansPath, desk.Loans);
    }

    /// <summary>借出、歸還之後只重寫借閱檔。名冊沒變就不碰它。</summary>
    public void SaveLoans(RentalDesk desk) => Write(LoansPath, desk.Loans);

    public RentalDesk Load() =>
        new(Read<Book>(BooksPath), Read<BookCopy>(CopiesPath), Read<Member>(MembersPath), Read<Loan>(LoansPath));

    private static void Write<T>(string path, IReadOnlyList<T> items)
    {
        // 直接對正本寫。寫到一半斷掉，正本就是半個檔案——第九天的主題。
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, items, Options);
    }

    private static List<T> Read<T>(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<List<T>>(stream, Options)
            ?? throw new InvalidDataException($"{path} 裡面不是一個 JSON 陣列。");
    }
}
