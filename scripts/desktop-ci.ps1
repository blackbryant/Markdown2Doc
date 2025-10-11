
# Window Environment
param(
  [string]$ProjectFile = "Markdown2Doc\Markdown2Doc.csproj",
  [string]$ThirdPartyDir = "Markdown2Doc\bin\Debug\net8.0-windows\",
  [string]$PublishDir = "installer\artifacts\win-x64",
  [string]$Runtime = "win-x64",
  [string]$Configuration = "Release",
  [string]$Version = "auto",           # auto 或 直接給 1.2.3
  [switch]$DoRelease                   # 本機僅影響檔名格式，不會上傳
)

$projRoot  = Split-Path $ProjectFile -Parent
$srcX64 = Join-Path $ThirdPartyDir "x64"
$srcX86 = Join-Path $ThirdPartyDir "x86"
$dstX64 = Join-Path $PublishDir "x64"
$dstX86 = Join-Path $PublishDir "x86"



$ErrorActionPreference = "Stop"

function Resolve-Version {
  param([string]$Version)

  # auto：用 run number + 短 SHA 模擬；在本機就用時間戳替代
  #$ts = Get-Date -Format "yyyyMMdd-HHmm"
  #$short = (git rev-parse --short=7 HEAD) 2>$null
  #if (-not $short) { $short = "local" }
  $repo = "blackbryant/Markdown2Doc"
  $relTag = gh release view --repo $repo --json tagName -q ".tagName"
  $Version = $relTag.Trim() -replace '^[vV]', ''
  
  return $Version
}

$version = Resolve-Version -Version $Version
Write-Host "==> Version: $version"

# 清理輸出
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null
New-Item -ItemType Directory -Force -Path "installer\artifacts" | Out-Null
New-Item -ItemType Directory -Force -Path "installer\output" | Out-Null

Write-Host "==> dotnet restore"
dotnet restore $ProjectFile

Write-Host "==> dotnet build"
dotnet build $ProjectFile `
  -c $Configuration `
  -p:Version=$version `
  -p:FileVersion=$version `
  -p:InformationalVersion=$version

Write-Host "==> dotnet publish (single-file, self-contained)"
dotnet publish $ProjectFile `
  -c $Configuration -r $Runtime --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=embedded `
  -p:Version=$version `
  -p:FileVersion=$version `
  -p:InformationalVersion=$version `
  -o $PublishDir

# 建立可攜式 zip
$portableName = if ($DoRelease) { "Markdown2Doc-portable-$version.zip" } else { "Markdown2Doc-portable-$version.zip" }
$portablePath = Join-Path "installer\artifacts" $portableName
if (Test-Path $portablePath) { Remove-Item $portablePath -Force }
Write-Host "==> Zip portable => $portablePath"
Compress-Archive -Path "$PublishDir\*" -DestinationPath $portablePath -Force

if (Test-Path $srcX64) {
  New-Item -ItemType Directory -Force -Path $dstX64 | Out-Null
  Copy-Item "$srcX64\*" $dstX64 -Recurse -Force
  Write-Host "==> Copied x64 native files to $dstX64"
} else {
  Write-Host "==> Skip: $srcX64 not found"
}

if (Test-Path $srcX86) {
  New-Item -ItemType Directory -Force -Path $dstX86 | Out-Null
  Copy-Item "$srcX86\*" $dstX86 -Recurse -Force
  Write-Host "==> Copied x86 native files to $dstX86"
} else {
  Write-Host "==> Skip: $srcX86 not found"
}


function Find-Iscc {
  $candidates = @()

  # 先從 PATH 尋找 iscc（相容 WinPS 5.1：不使用 ?.）
  try {
    $cmd = Get-Command iscc -ErrorAction Stop
    if ($cmd -and $cmd.Source -and (Test-Path $cmd.Source)) {
      return $cmd.Source
    }
  } catch {
    # 忽略，改試常見安裝路徑
  }

  # 常見安裝路徑（x64 / x86）
  if ($env:ProgramFiles) {
    $candidates += (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
  }
  if (${env:ProgramFiles(x86)}) {
    $candidates += (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe')
  }

  foreach ($p in $candidates | Get-Unique) {
    if (Test-Path $p) { return $p }
  }

  throw '找不到 Inno Setup 的 ISCC.exe。請先安裝 Inno Setup 6，或把其路徑加入 PATH。'
}

$IssPublishDir =  (Resolve-Path "$PublishDir\..").Path

$ISCC = Find-Iscc
Write-Host "==> Compile installer with Inno Setup"
& $ISCC /Qp `
  "/DMyAppVersion=$version" `
  "/DMyPublishDir=$IssPublishDir" `
  "installer\Markdown2Doc.iss"

Write-Host "==> Done."
Write-Host "Outputs:"
Get-ChildItem artifacts, installer\output -Recurse | Select-Object FullName,Length | Format-Table -AutoSize
