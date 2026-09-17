# highflyer-ironman

2026 iThome 鐵人賽《AI寫得出程式，但它不知道你的店：一家租書店的30天技術選型實錄》的程式碼。

一家單店的租書店，從紙本出租卡開始，三十天走到 SQLite。每一天對應一個 tag，取得出來、跑得起來，也可以和前一天比對差異。

文章在 iThome，作者 eugenesu0515。完整版教學在 [highflyer.info](https://highflyerinfo.com)。

## 需要什麼

.NET 10 SDK。版本釘在 `global.json`（`rollForward: latestFeature`，同主版本的更新也能跑）。

## 怎麼跑

```bash
dotnet test                             # 產生器的規則測試（根目錄有 Ironman.slnx）
dotnet run --project src/Ironman.Desk   # 第一版櫃檯程式
```

## 目前有什麼

| 專案 | 做什麼 |
|---|---|
| `src/Ironman.SeedData` | 種子資料產生器。固定隨機種子，同一個 seed 在任何機器上產生同一家店 |
| `src/Ironman.Desk` | 第一版櫃檯程式，資料只活在記憶體裡 |
| `tests/Ironman.SeedData.Tests` | 產生器不能破的規則 |

## 關於種子資料

`SeedDataGenerator.DefaultSeed` 是 `20260905`。同一個 seed、同一份詞庫，在任何機器上都產生同一位會員借走同一本書、同一天借的。詞庫改一個字，輸出就全變了。

`Loans()` 裡 `maxWaitDays` 那幾行有一段註解，記錄了第一版寫死「0～20 天」造成的問題：S 規模五百筆借閱在九月就排完，年底沒有任何未歸還，`有一部分借閱尚未歸還` 這條測試因此紅掉。想看那次失敗，把 `maxWaitDays` 改回固定 20 再跑一次 `dotnet test`。

## tag

| tag | 對應 |
|---|---|
| `day03` | 種子資料產生器＋第一版櫃檯 |

## 授權

程式碼 MIT。文章著作權保留。
