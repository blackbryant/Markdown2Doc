# 先本機跑你的 desktop-ci.ps1 產出 ZIP/EXE
#powershell -File .\scripts\desktop-ci.ps1 -Version auto -DoRelease -BuildInstaller

# 讀版本號（你腳本已回傳 ResolvedVersion，可寫入檔或用參數傳出）
$repo = "blackbryant/Markdown2Doc"
$ProjectFile = "Markdown2Doc\Markdown2Doc.csproj"
$projRoot  = Split-Path $ProjectFile -Parent

function Get-VersionFromProps {
  param([string]$propsPath = "$projRoot/version.props")
  if (-not (Test-Path $propsPath)) { throw "找不到 $propsPath" }
  [xml]$xml = Get-Content $propsPath
  $VersionTag = $xml.Project.PropertyGroup.VersionPrefix
  
  return $VersionTag 
}
 
$Version = Get-VersionFromProps
$Tag  = 'v'+$Version.Trim()  

$Tag
$Version
"installer\artifacts\Markdown2Doc-portable-$Version.zip"
"installer\output\Markdown2Doc-Setup-$Version.exe"
# 用 gh 建草稿 release 並上傳產物

gh auth login
gh release create $Tag `
  "installer\artifacts\Markdown2Doc-portable-$Version.zip" `
  "installer\output\Markdown2Doc-Setup-$Version.exe" `
  -t "Markdown2Doc $Tag" `
  -n "Changelog..." `
  --draft
 
  