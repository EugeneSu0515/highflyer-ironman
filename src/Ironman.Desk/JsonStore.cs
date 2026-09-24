using System.Text.Encodings.Web;
using System.Text.Json;
using Ironman.SeedData;

namespace Ironman.Desk;

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

    /// <summary>四個檔都在才算有資料；少一個就當成沒有，讓呼叫端決定要不要用種子資料重建。</summary>
    public bool Exists() =>
        File.Exists(BooksPath) && File.Exists(CopiesPath) && File.Exists(MembersPath) && File.Exists(LoansPath);

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
