namespace Ironman.SeedData;

/// <summary>
/// 產生格式正確的 ISBN-13（978 開頭、檢查碼有效）。
/// 只求「看起來像、可以驗證、不重複」，不對應任何真實書籍。
/// </summary>
public static class Isbn13
{
    public static string Generate(Random random)
    {
        // 978 + 9 位隨機數字 = 12 位，再算第 13 位檢查碼
        var digits = new int[13];
        digits[0] = 9;
        digits[1] = 7;
        digits[2] = 8;
        for (var i = 3; i < 12; i++)
        {
            digits[i] = random.Next(0, 10);
        }

        digits[12] = CheckDigit(digits);
        return string.Concat(digits);
    }

    /// <summary>ISBN-13 檢查碼：奇數位權重 1、偶數位權重 3，總和補到 10 的倍數。</summary>
    public static int CheckDigit(ReadOnlySpan<int> first12)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            sum += first12[i] * (i % 2 == 0 ? 1 : 3);
        }

        return (10 - sum % 10) % 10;
    }

    public static bool IsValid(string isbn)
    {
        if (isbn.Length != 13)
        {
            return false;
        }

        var digits = new int[13];
        for (var i = 0; i < 13; i++)
        {
            if (!char.IsAsciiDigit(isbn[i]))
            {
                return false;
            }

            digits[i] = isbn[i] - '0';
        }

        return CheckDigit(digits) == digits[12];
    }
}
