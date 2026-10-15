<#
.SYNOPSIS
    运行保存格式（ini / 元数据）的往返测试。

.DESCRIPTION
    IniFile 与 SlotMeta 既不依赖游戏也不依赖 Unity，
    所以可以单独当控制台程序跑。
    这些测试是为了不再出现「保存悄悄失败」这类问题。
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

$repo = Split-Path $PSScriptRoot -Parent
$src = Join-Path $repo "src"
$out = Join-Path $env:TEMP "lom_sse_tests.exe"

function Find-Csc {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -products * -prerelease -find "MSBuild\**\Bin\Roslyn\csc.exe" 2>$null
        if ($found) { return @($found)[0] }
    }
    return $null
}

# 本地的 csc 直呼路径根本不会解析 csproj，所以就算 csproj 坏了也发现不了，
# 一直到 CI 上才第一次失败。这里先把格式检查一遍。
# （历史上真在 XML 注释里写过连续两个减号把它写坏过）
Write-Host "检查项目文件..." -ForegroundColor Cyan
foreach ($proj in (Get-ChildItem -Path $repo -Filter *.csproj -Recurse -File)) {
    try {
        [xml](Get-Content -Path $proj.FullName -Raw) | Out-Null
        Write-Host ("  OK  " + $proj.FullName.Substring($repo.Length + 1))
    }
    catch {
        throw ("不是合法的 XML: " + $proj.FullName + "`n  " + $_.Exception.Message)
    }
}

$sources = @(
    (Join-Path $src "IniFile.cs")
    (Join-Path $src "SlotMeta.cs")
    (Join-Path $src "SlotOrder.cs")
    (Join-Path $PSScriptRoot "PersistenceTests.cs")
    (Join-Path $PSScriptRoot "SortTests.cs")
)

$csc = Find-Csc
if (-not $csc) {
    throw @"
找不到 csc.exe。请用下面两种方式之一运行。
  •安装 Visual Studio Build Tools 后重新运行
  •有 .NET SDK 的话用: dotnet run --project tests/Tests.csproj
"@
}

Write-Host "编译中..." -ForegroundColor Cyan
& $csc -nologo -target:exe -langversion:9.0 -out:"$out" @sources
if ($LASTEXITCODE -ne 0) { throw "测试编译失败 (exit $LASTEXITCODE)" }

Write-Host ""
& $out
$code = $LASTEXITCODE

Write-Host ""
if ($code -eq 0) { Write-Host "测试成功" -ForegroundColor Green }
else { Write-Host "测试失败" -ForegroundColor Red }
exit $code
