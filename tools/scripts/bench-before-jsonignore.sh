#!/usr/bin/env bash
# 重現第八天「修正前」那一欄：暫時把 Loan.IsReturned 上的 [JsonIgnore] 拿掉再量一次。
# 跑完會自己放回去，最後印出 git diff --stat 讓你確認原始碼沒有被留下改動。
set -e
cd "$(dirname "$0")/../.."

restore() { sed -i '' 's|^    //TEMP-NO-JSONIGNORE$|    [JsonIgnore]|' src/Ironman.SeedData/Models.cs; }
trap restore EXIT

sed -i '' 's|^    \[JsonIgnore\]$|    //TEMP-NO-JSONIGNORE|' src/Ironman.SeedData/Models.cs
grep -q 'TEMP-NO-JSONIGNORE' src/Ironman.SeedData/Models.cs || { echo "沒有換成功，停。"; exit 1; }

dotnet run -c Release --project tools/Ironman.Bench -- S 2>&1 | tee .runs/day08/bench-s-before.txt
dotnet run -c Release --project tools/Ironman.Bench -- M 2>&1 | tee .runs/day08/bench-m-before.txt

restore
trap - EXIT
echo "--- 原始碼應該沒有改動 ---"
git diff --stat src/Ironman.SeedData/Models.cs
