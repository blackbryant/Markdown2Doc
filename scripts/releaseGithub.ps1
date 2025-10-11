# 先本機跑你的 desktop-ci.ps1 產出 ZIP/EXE
#powershell -File .\scripts\desktop-ci.ps1 -Version auto -DoRelease -BuildInstaller

# 讀版本號（你腳本已回傳 ResolvedVersion，可寫入檔或用參數傳出）
$repo = "blackbryant/Markdown2Doc"
$tag = gh release view --repo $repo --json tagName -q ".tagName"
$Version = $tag.Trim() -replace '^[vV]', ''

$tag
$Version

# 用 gh 建草稿 release 並上傳產物
gh auth login
gh release create $tag `
  "installer\artifacts\Markdown2Doc-portable-$Version.zip" `
  "installer\output\Markdown2Doc-Setup-$Version.exe" `
  -t "Markdown2Doc $tag" `
  -n "Changelog..." `
  --draft