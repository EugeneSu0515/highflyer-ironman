namespace Ironman.SeedData;

/// <summary>
/// 產生可重現的種子資料：同一個 <see cref="Scale"/> 與 seed，在任何機器上都得到相同結果。
/// </summary>
/// <remarks>
/// 可重現性來自 <c>new Random(seed)</c>。有指定 seed 的 <see cref="Random"/> 使用固定演算法，
/// .NET 文件保證同一 seed 產生同一序列；沒有 seed 的 <c>new Random()</c> 則不保證。
/// 這是本專案唯一的隨機來源，所有集合都依固定順序產生，不使用平行處理。
/// </remarks>
public static class SeedDataGenerator
{
    /// <summary>系列預設 seed。文章裡的所有數字都以這個 seed 產生。</summary>
    public const int DefaultSeed = 20260905;

    /// <summary>借閱期間的結束日。開始日依規模回推，見 <see cref="PeriodFor"/>。</summary>
    public static readonly DateOnly PeriodEnd = new(2025, 12, 31);

    /// <summary>
    /// 借閱期間。每本書一年最多排得下十來筆借閱（平均週期約 26 天），
    /// 所以期間長度依「每本書平均借閱次數」回推，並保留 1.5 倍餘裕；最短一年。
    /// S 約一年、M 約三年、L 約六年——這也符合系列故事：資料量是店開了幾年後累積出來的。
    /// </summary>
    public static (DateOnly Start, DateOnly End) PeriodFor(Scale scale)
    {
        var profile = ScaleProfile.For(scale);
        var loansPerBook = (double)profile.Loans / profile.Books;
        var days = Math.Max(365, (int)Math.Ceiling(loansPerBook * AverageCycleDays * 1.5));
        return (PeriodEnd.AddDays(-days), PeriodEnd);
    }

    private const int AverageCycleDays = 26;      // 平均等待 10 天 + 平均借閱 15.5 天，用於回推期間長度
    private const double AverageLoanDays = 15.5;  // 借閱 1～30 天的平均

    public static SeedDataSet Generate(Scale scale, int seed = DefaultSeed)
    {
        var profile = ScaleProfile.For(scale);
        var random = new Random(seed);

        var books = GenerateBooks(random, profile.Books);
        var copies = GenerateCopies(random, books);
        var members = GenerateMembers(random, profile.Members);
        var (periodStart, periodEnd) = PeriodFor(scale);
        var loans = GenerateLoans(random, copies, members, profile.Loans, periodStart, periodEnd);

        return new SeedDataSet(scale, seed, books, copies, members, loans);
    }

    // ---------------------------------------------------------------- Books

    private static List<Book> GenerateBooks(Random random, int count)
    {
        var books = new List<Book>(count);
        var seen = new HashSet<string>(count);

        while (books.Count < count)
        {
            var isbn = Isbn13.Generate(random);
            if (!seen.Add(isbn))
            {
                continue; // 極少見的重複，重抽一次
            }

            var title = Pick(random, Vocabulary.TitleHeads) + Pick(random, Vocabulary.TitleTails);
            var author = Pick(random, Vocabulary.Surnames) + Pick(random, Vocabulary.GivenNames);
            books.Add(new Book(isbn, title, author));
        }

        return books;
    }

    // --------------------------------------------------------------- Copies

    /// <summary>
    /// 館藏副本。每種書至少一本；約 15% 的書有第二本、約 3% 有第三本——這就是 #02 的觸發事件。
    /// 條碼編號 C00001 起依書目順序連號，貼在書背上，店員掃的是它。
    /// </summary>
    private static List<BookCopy> GenerateCopies(Random random, IReadOnlyList<Book> books)
    {
        var copies = new List<BookCopy>(books.Count + books.Count / 5);
        var next = 1;

        foreach (var book in books)
        {
            var count = 1;
            var roll = random.Next(100);
            if (roll < 3)
            {
                count = 3;
            }
            else if (roll < 15)
            {
                count = 2;
            }

            for (var i = 0; i < count; i++)
            {
                copies.Add(new BookCopy($"C{next++:D5}", book.Isbn));
            }
        }

        return copies;
    }

    // -------------------------------------------------------------- Members

    private static List<Member> GenerateMembers(Random random, int count)
    {
        var members = new List<Member>(count);

        for (var i = 1; i <= count; i++)
        {
            var id = $"M{i:D5}";
            var name = Pick(random, Vocabulary.Surnames) + Pick(random, Vocabulary.GivenNames);
            var phone = $"09{random.Next(10, 99)}-{random.Next(0, 1000):D3}-{random.Next(0, 1000):D3}";
            members.Add(new Member(id, name, phone));
        }

        return members;
    }

    // ---------------------------------------------------------------- Loans

    /// <summary>
    /// 借閱紀錄的兩個不變量：
    /// 1. 同一本副本（同一 CopyId）的借閱期間不重疊——一本實體書借出去就沒有第二本。
    /// 2. 歸還日不早於借出日。
    /// 產生方式：每本副本維護「下一次可借出的日期」，逐筆往後排；最後依借出日排序並給流水號。
    /// </summary>
    private static List<Loan> GenerateLoans(
        Random random,
        IReadOnlyList<BookCopy> copies,
        IReadOnlyList<Member> members,
        int count,
        DateOnly periodStart,
        DateOnly periodEnd)
    {
        var drafts = new List<(string MemberId, string CopyId, DateOnly LoanDate, DateOnly? ReturnDate)>(count);
        var nextAvailable = new DateOnly[copies.Count];
        Array.Fill(nextAvailable, periodStart);

        // 兩次借出之間的等待天數上限，依「每本副本要在期間內排進幾筆」回推，
        // 讓借閱平均分布在整個期間，而不是擠在期間開頭。
        // 第一版固定 0～20 天，S 規模的 500 筆在九月就排完，年底沒有任何未歸還——測試抓到了這件事。
        var periodDays = periodEnd.DayNumber - periodStart.DayNumber;
        var loansPerCopy = (double)count / copies.Count;
        var slotDays = periodDays * 0.85 / loansPerCopy;            // 每筆借閱平均佔用的天數（留 15% 餘裕）
        var maxWaitDays = Math.Max(1, (int)(2 * (slotDays - AverageLoanDays)));

        var guard = 0;

        while (drafts.Count < count)
        {
            var copyIndex = random.Next(copies.Count);
            var member = members[random.Next(members.Count)];

            // 從這本副本可借出的日期起，等待 0～maxWaitDays 天後借出
            var loanDate = nextAvailable[copyIndex].AddDays(random.Next(0, maxWaitDays + 1));
            if (loanDate > periodEnd)
            {
                // 這本副本在期間內已經借滿，換一本；guard 避免極端 seed 下無限迴圈
                if (++guard > count * 10)
                {
                    throw new InvalidOperationException("借閱期間內排不下這麼多筆借閱，請放寬期間或縮小規模。");
                }
                continue;
            }

            // 借閱天數 1～30 天；約 15% 尚未歸還（只在期間尾端才合理）
            var days = random.Next(1, 31);
            var returnDate = loanDate.AddDays(days);
            DateOnly? actualReturn = returnDate;

            if (returnDate > periodEnd || (random.Next(100) < 15 && periodEnd.DayNumber - loanDate.DayNumber < 45))
            {
                actualReturn = null; // 尚未歸還
                nextAvailable[copyIndex] = periodEnd.AddDays(1); // 這本副本在期間內不再借出
            }
            else
            {
                nextAvailable[copyIndex] = returnDate.AddDays(1);
            }

            drafts.Add((member.MemberId, copies[copyIndex].CopyId, loanDate, actualReturn));
        }

        // 依借出日排序，讓輸出像一疊按時間插入的卡片；流水號照這個順序給，1 起算
        drafts.Sort(static (a, b) => a.LoanDate.CompareTo(b.LoanDate));
        var loans = new List<Loan>(count);
        for (var i = 0; i < drafts.Count; i++)
        {
            var d = drafts[i];
            loans.Add(new Loan(i + 1, d.MemberId, d.CopyId, d.LoanDate, d.ReturnDate));
        }

        return loans;
    }

    private static string Pick(Random random, string[] source) => source[random.Next(source.Length)];
}
