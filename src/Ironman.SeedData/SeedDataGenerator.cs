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
        var members = GenerateMembers(random, profile.Members);
        var (periodStart, periodEnd) = PeriodFor(scale);

        // 借閱先對「書目」排，抽籤順序和 #01 完全一樣：同一個 seed 的會員名冊、借出日、
        // 每本書的借閱次數都不會變，前面幾篇引用過的名字與排行榜仍然成立。
        var drafts = GenerateLoanDrafts(random, books, members, profile.Loans, periodStart, periodEnd);

        // 店裡進貨看的是哪幾本借得最兇，所以副本數依借閱次數決定，不擲骰。
        var copies = GenerateCopies(books, drafts);

        // 最後把每一筆借閱綁到那本書的其中一本實體書。
        var loans = BindLoansToCopies(books, copies, drafts);

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

    /// <summary>
    /// 借閱草稿：這個階段還不知道是哪一本實體書，只知道是哪一本書目。
    /// </summary>
    private readonly record struct LoanDraft(int BookIndex, string MemberId, DateOnly LoanDate, DateOnly? ReturnDate);

    private static List<LoanDraft> GenerateLoanDrafts(
        Random random,
        IReadOnlyList<Book> books,
        IReadOnlyList<Member> members,
        int count,
        DateOnly periodStart,
        DateOnly periodEnd)
    {
        var drafts = new List<LoanDraft>(count);
        var nextAvailable = new DateOnly[books.Count];
        Array.Fill(nextAvailable, periodStart);

        // 兩次借出之間的等待天數上限，依「每本書要在期間內排進幾筆」回推，
        // 讓借閱平均分布在整個期間，而不是擠在期間開頭。
        var periodDays = periodEnd.DayNumber - periodStart.DayNumber;
        var loansPerBook = (double)count / books.Count;
        var slotDays = periodDays * 0.85 / loansPerBook;            // 每筆借閱平均佔用的天數（留 15% 餘裕）
        var maxWaitDays = Math.Max(1, (int)(2 * (slotDays - AverageLoanDays)));

        var guard = 0;

        while (drafts.Count < count)
        {
            var bookIndex = random.Next(books.Count);
            var member = members[random.Next(members.Count)];

            var loanDate = nextAvailable[bookIndex].AddDays(random.Next(0, maxWaitDays + 1));
            if (loanDate > periodEnd)
            {
                if (++guard > count * 10)
                {
                    throw new InvalidOperationException("借閱期間內排不下這麼多筆借閱，請放寬期間或縮小規模。");
                }
                continue;
            }

            var days = random.Next(1, 31);
            var returnDate = loanDate.AddDays(days);
            DateOnly? actualReturn = returnDate;

            if (returnDate > periodEnd || (random.Next(100) < 15 && periodEnd.DayNumber - loanDate.DayNumber < 45))
            {
                actualReturn = null;
                nextAvailable[bookIndex] = periodEnd.AddDays(1);
            }
            else
            {
                nextAvailable[bookIndex] = returnDate.AddDays(1);
            }

            drafts.Add(new LoanDraft(bookIndex, member.MemberId, loanDate, actualReturn));
        }

        drafts.Sort(static (a, b) => a.LoanDate.CompareTo(b.LoanDate));
        return drafts;
    }

    /// <summary>
    /// 館藏副本：條碼 C00001 起依書目順序連號，貼在書背上，店員掃的是它。
    /// 副本數依借閱次數決定——借得最兇的那幾本，老闆才會再進一本。
    /// </summary>
    private static List<BookCopy> GenerateCopies(IReadOnlyList<Book> books, IReadOnlyList<LoanDraft> drafts)
    {
        var loanCount = new int[books.Count];
        foreach (var d in drafts)
        {
            loanCount[d.BookIndex]++;
        }

        // 前 3% 進到三本、前 15% 進到兩本；門檻用借閱次數排序取分位數，
        // 同分的書一起進場，所以實際本數可能略多於門檻。
        var sorted = loanCount.OrderByDescending(c => c).ToArray();
        var threeAt = sorted[Math.Min(sorted.Length - 1, Math.Max(0, books.Count * 3 / 100 - 1))];
        var twoAt = sorted[Math.Min(sorted.Length - 1, Math.Max(0, books.Count * 15 / 100 - 1))];

        var copies = new List<BookCopy>(books.Count + books.Count / 5);
        var next = 1;

        for (var i = 0; i < books.Count; i++)
        {
            var count = loanCount[i] >= threeAt ? 3 : loanCount[i] >= twoAt ? 2 : 1;
            for (var k = 0; k < count; k++)
            {
                copies.Add(new BookCopy($"C{next++:D5}", books[i].Isbn));
            }
        }

        return copies;
    }

    /// <summary>
    /// 把每一筆借閱綁到那本書的其中一本實體書，依借出日輪流分配。
    /// 草稿階段同一本書的借閱期間本來就不重疊，拆到副本之後只會更鬆。
    /// </summary>
    private static List<Loan> BindLoansToCopies(
        IReadOnlyList<Book> books,
        IReadOnlyList<BookCopy> copies,
        IReadOnlyList<LoanDraft> drafts)
    {
        var copiesOf = new List<BookCopy>[books.Count];
        for (var i = 0; i < books.Count; i++)
        {
            copiesOf[i] = [];
        }

        var indexOfIsbn = new Dictionary<string, int>(books.Count);
        for (var i = 0; i < books.Count; i++)
        {
            indexOfIsbn[books[i].Isbn] = i;
        }

        foreach (var c in copies)
        {
            copiesOf[indexOfIsbn[c.Isbn]].Add(c);
        }

        var cursor = new int[books.Count];
        var loans = new List<Loan>(drafts.Count);

        for (var i = 0; i < drafts.Count; i++)
        {
            var d = drafts[i];
            var list = copiesOf[d.BookIndex];
            var copy = list[cursor[d.BookIndex]++ % list.Count];
            loans.Add(new Loan(i + 1, d.MemberId, copy.CopyId, d.LoanDate, d.ReturnDate));
        }

        return loans;
    }


    // ---------------------------------------------------------------- Loans

    /// <summary>
    /// 借閱紀錄的兩個不變量：
    /// 1. 同一本副本（同一 CopyId）的借閱期間不重疊——一本實體書借出去就沒有第二本。
    /// 2. 歸還日不早於借出日。
    /// 產生方式：每本副本維護「下一次可借出的日期」，逐筆往後排；最後依借出日排序並給流水號。
    /// </summary>

    private static string Pick(Random random, string[] source) => source[random.Next(source.Length)];
}
