namespace Ironman.SeedData;

/// <summary>
/// 資料規模。整個系列的量測都用同一組定義，數字不隨階段改變。
/// </summary>
public enum Scale
{
    /// <summary>教學示範與單元測試：100 本書、40 位會員、500 筆借閱。</summary>
    S,

    /// <summary>量測檔案掃描成本：2,000 本書、600 位會員、50,000 筆借閱。</summary>
    M,

    /// <summary>量測索引與資料庫效益：20,000 本書、5,000 位會員、1,000,000 筆借閱。</summary>
    L,
}

/// <summary>每個規模的實體數量。</summary>
public readonly record struct ScaleProfile(int Books, int Members, int Loans)
{
    public static ScaleProfile For(Scale scale) => scale switch
    {
        Scale.S => new(Books: 100, Members: 40, Loans: 500),
        Scale.M => new(Books: 2_000, Members: 600, Loans: 50_000),
        Scale.L => new(Books: 20_000, Members: 5_000, Loans: 1_000_000),
        _ => throw new ArgumentOutOfRangeException(nameof(scale), scale, "未定義的規模"),
    };
}
