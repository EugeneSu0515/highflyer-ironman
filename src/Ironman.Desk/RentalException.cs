namespace Ironman.Desk;

/// <summary>
/// 櫃檯操作違反營運規則時丟出。訊息直接給店員看，所以用中文、講人話。
/// 這不是程式錯誤（那種讓它直接炸出來），而是「這件事不能做」。
/// </summary>
public sealed class RentalException(string message) : Exception(message);
