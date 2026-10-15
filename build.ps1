<#
.SYNOPSIS
    构建 LOM 存档位扩展（汉化版）。

.DESCRIPTION
    有 .NET SDK 就用 SDK，没有就直接调用 Visual Studio / Build Tools 自带的
    Roslyn（csc.exe）。两者都没有则报错。

    游戏位置按 -GameRoot、环境变量 LOM_GAME_ROOT、Steam 注册信息的顺序查找。

.PARAMETER GameRoot
    《活侠传》（LegendOfMortal）的安装目录，也就是 Mortal.exe 所在的位置。

.PARAMETER Deploy
    构建完成后，复制到游戏的 BepInEx\plugins\LOM_SaveSlotExpander。

.PARAMETER Verify
    构建完成后，用 tools/PatchCheck 核对补丁目标是否还认得当前版本的游戏。

.PARAMETER Package
    构建完成后，生成可以直接解压到游戏根目录的发布包
    （dist\LOM_SaveSlotExpander-zh-v<版本>.zip）。

.EXAMPLE
    pwsh -File build.ps1
    pwsh -File build.ps1 -Deploy
    pwsh -File build.ps1 -GameRoot "D:\Steam\steamapps\common\LegendOfMortal" -Deploy -Verify -Package
#>
[CmdletBinding()]
param(
    [string]$GameRoot,
    [switch]$Deploy,
    [switch]$Verify,
    [switch]$Package
)

$ErrorActionPreference = "Stop"
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

$repo    = $PSScriptRoot
$srcDir  = Join-Path $repo "src"
$distDir = Join-Path $repo "dist\LOM_SaveSlotExpander"
$outDll  = Join-Path $distDir "LOM_SaveSlotExpander.dll"

# ---------------------------------------------------------------- 游戏位置

function Test-GameRoot([string]$path) {
    if ([string]::IsNullOrWhiteSpace($path)) { return $false }
    return (Test-Path (Join-Path $path "Mortal_Data\Managed\Mortal.Core.dll"))
}

function Find-GameRoot {
    if (Test-GameRoot $script:GameRoot) { return $script:GameRoot }
    if (Test-GameRoot $env:LOM_GAME_ROOT) { return $env:LOM_GAME_ROOT }

    # 依次查看 Steam 的各个库
    $libraries = @()
    try {
        $steamPath = (Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -EA Stop).SteamPath
        if ($steamPath) {
            $libraries += $steamPath
            $vdf = Join-Path $steamPath "steamapps\libraryfolders.vdf"
            if (Test-Path $vdf) {
                foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s*"([^"]+)"')) {
                    $libraries += $m.Groups[1].Value -replace '\\\\', '\'
                }
            }
        }
    } catch { }

    foreach ($lib in $libraries) {
        $candidate = Join-Path $lib "steamapps\common\LegendOfMortal"
        if (Test-GameRoot $candidate) { return $candidate }
    }

    # 常见位置逐个试
    foreach ($drive in (Get-PSDrive -PSProvider FileSystem).Name) {
        foreach ($suffix in @("SteamLibrary\steamapps\common\LegendOfMortal",
                              "Program Files (x86)\Steam\steamapps\common\LegendOfMortal",
                              "Steam\steamapps\common\LegendOfMortal")) {
            $candidate = "${drive}:\$suffix"
            if (Test-GameRoot $candidate) { return $candidate }
        }
    }

    return $null
}

$GameRoot = Find-GameRoot
if (-not $GameRoot) {
    throw @"
找不到《活侠传》的安装目录。
请用 -GameRoot 明确指定，或设置环境变量 LOM_GAME_ROOT。
  例: pwsh -File build.ps1 -GameRoot "D:\Steam\steamapps\common\LegendOfMortal"
"@
}
Write-Host "游戏目录 : $GameRoot"

$managed = Join-Path $GameRoot "Mortal_Data\Managed"
$bepCore = Join-Path $GameRoot "BepInEx\core"

if (-not (Test-Path (Join-Path $bepCore "BepInEx.Unity.Mono.dll"))) {
    throw "找不到 BepInEx: $bepCore`n请先安装 BepInEx 6（Unity Mono 版）。"
}

# ------------------------------------------------------------------ 引用列表

$references = @(
    "$managed\mscorlib.dll"
    "$managed\System.dll"
    "$managed\System.Core.dll"
    "$managed\netstandard.dll"
    "$managed\UnityEngine.dll"
    "$managed\UnityEngine.CoreModule.dll"
    "$managed\UnityEngine.UI.dll"
    "$managed\UnityEngine.UIModule.dll"
    "$managed\UnityEngine.TextRenderingModule.dll"
    "$managed\UnityEngine.InputLegacyModule.dll"
    "$managed\Unity.InputSystem.dll"
    "$managed\Mortal.Core.dll"
    "$bepCore\BepInEx.Core.dll"
    "$bepCore\BepInEx.Unity.Mono.dll"
    "$bepCore\BepInEx.Unity.Common.dll"
    "$bepCore\0Harmony.dll"
)

$missing = $references | Where-Object { -not (Test-Path $_) }
if ($missing) { throw "找不到这些引用程序集:`n  " + ($missing -join "`n  ") }

# ------------------------------------------------------------------ 编译器

function Find-Csc {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -products * -prerelease -find "MSBuild\**\Bin\Roslyn\csc.exe" 2>$null
        if ($found) { return @($found)[0] }
    }
    foreach ($root in @("${env:ProgramFiles}\Microsoft Visual Studio", "${env:ProgramFiles(x86)}\Microsoft Visual Studio")) {
        if (-not (Test-Path $root)) { continue }
        $found = Get-ChildItem -Path $root -Filter csc.exe -Recurse -EA SilentlyContinue |
                 Where-Object { $_.FullName -like "*\Bin\Roslyn\csc.exe" } |
                 Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    return $null
}

$sources = Get-ChildItem -Path $srcDir -Filter *.cs | ForEach-Object { $_.FullName }
if (-not $sources) { throw "找不到源码: $srcDir" }

if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir -Force | Out-Null }

$hasSdk = $false
if (Get-Command dotnet -EA SilentlyContinue) {
    $hasSdk = [bool](& dotnet --list-sdks 2>$null)
}

if ($hasSdk) {
    Write-Host "编译器   : dotnet SDK"
    & dotnet build (Join-Path $srcDir "LOM_SaveSlotExpander.csproj") `
        -c Release -p:GameRoot="$GameRoot" -p:OutputPath="$distDir\" --nologo
    if ($LASTEXITCODE -ne 0) { throw "编译失败 (exit $LASTEXITCODE)" }
}
else {
    $csc = Find-Csc
    if (-not $csc) {
        throw @"
找不到编译器。请安装下面任意一项。
  •.NET SDK            https://dotnet.microsoft.com/download
  •Visual Studio Build Tools（勾选「.NET 桌面生成工具」）
"@
    }
    Write-Host "编译器   : $csc"

    $cscArgs = @(
        "/target:library"
        "/out:$outDll"
        "/langversion:9.0"   # Unity 的 Mono 里没有 C#10 需要的运行时辅助类型
        "/nostdlib+"
        "/noconfig"
        "/optimize+"
        "/debug-"
        "/nologo"
    )
    $cscArgs += ($references | ForEach-Object { "/reference:$_" })
    $cscArgs += $sources

    & $csc @cscArgs
    if ($LASTEXITCODE -ne 0) { throw "编译失败 (exit $LASTEXITCODE)" }
}

Write-Host "已生成   : $outDll" -ForegroundColor Green

# -------------------------------------------------------------------- 核对

if ($Verify) {
    Write-Host ""
    Write-Host "核对补丁目标..." -ForegroundColor Cyan
    & dotnet run --project (Join-Path $repo "tools\PatchCheck") -- "$GameRoot" "$outDll"
    if ($LASTEXITCODE -ne 0) { throw "核对未通过 (exit $LASTEXITCODE)" }
}

# -------------------------------------------------------------------- 部署

if ($Deploy) {
    $pluginDir = Join-Path $GameRoot "BepInEx\plugins\LOM_SaveSlotExpander"
    if (-not (Test-Path $pluginDir)) { New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null }
    Copy-Item $outDll -Destination $pluginDir -Force
    Write-Host "已部署   : $pluginDir" -ForegroundColor Green
}

# -------------------------------------------------------------------- 打包

if ($Package) {
    # 版本号直接从源码里取，免得手写两处对不上
    $m = [regex]::Match((Get-Content (Join-Path $srcDir "Plugin.cs") -Raw), 'VERSION\s*=\s*"([^"]+)"')
    if (-not $m.Success) { throw "无法从 src\Plugin.cs 里读出 VERSION。" }
    $version = $m.Groups[1].Value

    # 压缩包里直接带上 BepInEx 的目录结构，用户解压到游戏根目录就装好了
    $stage = Join-Path $env:TEMP ("lom_sse_package_" + [Guid]::NewGuid().ToString("N"))
    $stagePlugin = Join-Path $stage "BepInEx\plugins\LOM_SaveSlotExpander"
    New-Item -ItemType Directory -Path $stagePlugin -Force | Out-Null
    Copy-Item $outDll -Destination $stagePlugin -Force

    $notice = @"
LOM 存档位扩展（汉化版） $version
《活侠传》存档位 20 → 999，并且可以给每个存档位写中文注释

【安装】
把压缩包里的 BepInEx 文件夹解压（与原文件夹合并）到《活侠传》的游戏根目录，
也就是 Mortal.exe 所在的那个文件夹。解压后应当出现下面这个文件，这就是装对了：

    BepInEx\plugins\LOM_SaveSlotExpander\LOM_SaveSlotExpander.dll

游戏根目录在哪：Steam 库里右键《活侠传》→ 管理 → 浏览本地文件。

【需要什么】
BepInEx 6（Unity Mono 版）。如果游戏根目录下没有 BepInEx 文件夹，说明还没装，
请先去 BepInEx 的发布页装好 6.x 的 Unity Mono 版本。
不需要安装任何日语化 / 本地化 MOD。

【用法】
打开存档或读档界面，把鼠标移到（或用光标选中）存档位上：
    F2    写注释，Enter 确定 / Esc 取消
    F3    在「按编号」与「按存档时间」之间切换排序

【设置】
BepInEx\config\lom.zh.saveslotexpander.cfg，首次启动游戏后生成。
注释内容存在存档文件夹里的 SaveSlotComments.ini，可以用文本编辑器直接改。

【卸载】
删掉 BepInEx\plugins\LOM_SaveSlotExpander 文件夹即可，不影响存档数据。

项目主页：https://github.com/shiluodeqiou/LOM_SaveSlotExpander-zh
"@
    # 统一成 CRLF 再写：这是给 Windows 用户直接打开的文本
    $notice = ($notice -replace "`r?`n", "`r`n")
    # 带 BOM 写出，用记事本打开不会乱码
    [System.IO.File]::WriteAllText((Join-Path $stage "安装说明.txt"), $notice,
                                   (New-Object System.Text.UTF8Encoding($true)))

    $zip = Join-Path $repo ("dist\LOM_SaveSlotExpander-zh-v" + $version + ".zip")
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip -Force
    Remove-Item $stage -Recurse -Force

    Write-Host "已打包   : $zip" -ForegroundColor Green
    Write-Host "           （解压到游戏根目录即可，已含 BepInEx\plugins 目录结构）"
}
