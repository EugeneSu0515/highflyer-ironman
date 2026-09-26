#!/usr/bin/env bash
# 第八天「少一個檔」那一幕：把刪檔前後的檔案狀態一起留下來，
# 這樣畫面本身就證明得了「剩下的檔沒有被動過」，不必讀者相信程式印的那兩行字。
set -e
cd "$(dirname "$0")/../.."
DATA=src/Ironman.Desk/bin/Release/net10.0/data

rm -rf "$DATA"
printf '1\nM00001\nC00099\n0\n' | dotnet run -c Release --project src/Ironman.Desk > /dev/null

echo "--- 刪檔前 ---"
( cd "$DATA" && ls -l *.json | awk '{print $5, $6, $7, $8, $9}' && shasum *.json )

mv "$DATA/members.json" /tmp/members.json.keep
echo
echo "--- 少了 members.json，再開一次 ---"
dotnet run -c Release --project src/Ironman.Desk
echo
echo "--- 啟動之後 ---"
( cd "$DATA" && ls -l *.json | awk '{print $5, $6, $7, $8, $9}' && shasum *.json )
mv /tmp/members.json.keep "$DATA/members.json"
