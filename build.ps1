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
    包里会一并带上 BepInEx 的运行时文件，所以只装这一个 mod 的用户也能直接用。

.PARAMETER BepInExSource
    打包时从哪里取 BepInEx 运行时。默认就用 -GameRoot 指定的游戏目录
    （那里本来就是一份装好的 BepInEx）。只有想改用别处的干净副本时才需要指定。

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
    [switch]$Package,
    [string]$BepInExSource
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

    # BepInEx 运行时的来源。默认用游戏目录里的那份，它本来就是装好的
    if (-not $BepInExSource) { $BepInExSource = $GameRoot }
    if (-not (Test-Path $BepInExSource)) { throw "BepInEx 来源目录不存在: $BepInExSource" }

    # ---- 只取 BepInEx 自己的运行时文件 ----
    # 这里必须用白名单而不是"排除法"：游戏目录的 BepInEx\plugins 和 BepInEx\config
    # 里放着别人的 mod 和别人的配置，一旦被打进发布包就是事故。
    # 这份清单与官方 BepInEx 6 Unity.Mono 发布包的内容一一对应。
    $bepRootFiles = @("winhttp.dll", "doorstop_config.ini", ".doorstop_version", "changelog.txt")
    $bepDirs      = @("BepInEx\core")

    foreach ($f in $bepRootFiles) {
        if (-not (Test-Path (Join-Path $BepInExSource $f))) {
            throw "在 $BepInExSource 里找不到 BepInEx 的 $f，无法打出「解压即用」的包。"
        }
    }
    foreach ($d in $bepDirs) {
        if (-not (Test-Path (Join-Path $BepInExSource $d))) {
            throw "在 $BepInExSource 里找不到 BepInEx 的 $d 目录。"
        }
    }

    $stage = Join-Path $env:TEMP ("lom_sse_package_" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $stage -Force | Out-Null

    # 1) BepInEx 运行时（让只装这一个 mod 的用户也能直接跑起来）
    foreach ($f in $bepRootFiles) {
        Copy-Item (Join-Path $BepInExSource $f) -Destination $stage -Force
    }
    foreach ($d in $bepDirs) {
        $dst = Join-Path $stage $d
        New-Item -ItemType Directory -Path $dst -Force | Out-Null
        Copy-Item (Join-Path $BepInExSource "$d\*") -Destination $dst -Recurse -Force
    }

    # 2) 本 mod 本体
    $stagePlugin = Join-Path $stage "BepInEx\plugins\LOM_SaveSlotExpander"
    New-Item -ItemType Directory -Path $stagePlugin -Force | Out-Null
    Copy-Item $outDll -Destination $stagePlugin -Force

    # 3) 第三方组件的许可（BepInEx 与 Doorstop 都是 LGPL-2.1，随包分发必须带上）
    $licDir = Join-Path $stage "第三方许可"
    New-Item -ItemType Directory -Path $licDir -Force | Out-Null
    foreach ($lic in @("BepInEx-LICENSE.txt", "Doorstop-LICENSE.txt")) {
        $src = Join-Path $repo ("third_party\" + $lic)
        if (Test-Path $src) { Copy-Item $src -Destination $licDir -Force }
    }

    # 包里自带的这个 BepInEx 是哪个版本，写进说明里
    $bepVer = "6.x"
    try {
        $vi = (Get-Item (Join-Path $BepInExSource "BepInEx\core\BepInEx.Core.dll")).VersionInfo
        foreach ($cand in @($vi.ProductVersion, $vi.FileVersion)) {
            if ($cand) { $bepVer = ($cand -split "\+")[0]; break }   # 去掉 "+提交号" 那段
        }
    } catch { }
    $doorstopVer = "未知"
    try { $doorstopVer = (Get-Content (Join-Path $BepInExSource ".doorstop_version") -Raw).Trim() } catch { }

    # 4) 安装说明
    $notice = @"
LOM 存档位扩展（汉化版） $version
《活侠传》存档位 20 → 999，并且可以给每个存档位写中文注释

【安装】只要两步
1. 在 Steam 库里右键《活侠传》→ 管理 → 浏览本地文件，打开游戏根目录
   （就是 Mortal.exe 所在的那个文件夹）
2. 把本压缩包的**全部内容**解压进去，与原文件夹合并、提示覆盖就选覆盖

解压后应当出现下面这些文件，这就是装对了：

    winhttp.dll
    doorstop_config.ini
    BepInEx\core\BepInEx.Unity.Mono.Preloader.dll
    BepInEx\plugins\LOM_SaveSlotExpander\LOM_SaveSlotExpander.dll

【关于 BepInEx】
本包已经自带 BepInEx 运行时（BepInEx $bepVer / Doorstop $doorstopVer，LGPL-2.1），
所以就算你之前什么 mod 都没装过，解压完也能直接玩。不需要再去别处下载。

如果你已经装过 BepInEx，直接覆盖一般也没问题。但假如你那份比本包里的新，
建议只解压 BepInEx\plugins 这个文件夹，免得把新版换成旧版。

其它 mod 互不影响：各家的插件各自放在 BepInEx\plugins 下自己的文件夹里，
安装新 mod 时继续往 BepInEx\plugins 里解压即可。

【用法】
打开存档或读档界面，把鼠标移到（或用光标选中）存档位上：
    F2    写注释，Enter 确定 / Esc 取消
    F3    在「按编号」与「按存档时间」之间切换排序

【设置】
BepInEx\config\lom.zh.saveslotexpander.cfg，首次启动游戏后生成。
注释内容存在存档文件夹里的 SaveSlotComments.ini，可以用文本编辑器直接改。

【卸载】
只删 BepInEx\plugins\LOM_SaveSlotExpander 文件夹，不影响存档，
其它 mod 也照常。想彻底清掉 BepInEx 的话再删 BepInEx 文件夹、winhttp.dll、
doorstop_config.ini、.doorstop_version。

项目主页：https://github.com/shiluodeqiou/LOM_SaveSlotExpander-zh
"@
    # 统一成 CRLF 再写：这是给 Windows 用户直接打开的文本
    $notice = ($notice -replace "`r?`n", "`r`n")
    # 带 BOM 写出，用记事本打开不会乱码
    [System.IO.File]::WriteAllText((Join-Path $stage "安装说明.txt"), $notice,
                                   (New-Object System.Text.UTF8Encoding($true)))

    # 5) 第三方组件说明
    $thirdParty = @"
本发布包中包含以下第三方组件，它们不是本项目（LOM 存档位扩展 汉化版）的一部分，
随包分发只是为了让你解压一次就能直接使用。完整许可原文见「第三方许可」文件夹。

  BepInEx（含 Harmony / MonoMod / Mono.Cecil 等）
      版本  : $bepVer
      许可  : GNU LGPL-2.1
      来源  : https://github.com/BepInEx/BepInEx

  UnityDoorstop
      版本  : $doorstopVer
      许可  : GNU LGPL-2.1
      来源  : https://github.com/NeighTools/UnityDoorstop

本项目的源码与许可（MIT，继承自原版 LOM_SaveSlotExpander）：
  https://github.com/shiluodeqiou/LOM_SaveSlotExpander-zh
原版：https://gitlab.com/ame-chan/lom_saveslotexpander
"@
    $thirdParty = ($thirdParty -replace "`r?`n", "`r`n")
    [System.IO.File]::WriteAllText((Join-Path $stage "第三方组件与许可.txt"), $thirdParty,
                                   (New-Object System.Text.UTF8Encoding($true)))

    # 6) 压缩。安全校验：确认没有把别人的 mod / 配置带进去
    $strayPlugins = @(Get-ChildItem (Join-Path $stage "BepInEx\plugins") -Directory |
                      Where-Object { $_.Name -ne "LOM_SaveSlotExpander" })
    $strayConfigs = @(Get-ChildItem (Join-Path $stage "BepInEx\config") -File -EA SilentlyContinue)
    if ($strayPlugins.Count -gt 0 -or $strayConfigs.Count -gt 0) {
        throw "暂存目录里混进了别的 mod 或配置，已中止。plugins: $($strayPlugins.Name -join ', ')"
    }

    $fileCount = @(Get-ChildItem $stage -Recurse -File -Force).Count

    $zip = Join-Path $repo ("dist\LOM_SaveSlotExpander-zh-v" + $version + ".zip")
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip -Force
    Remove-Item $stage -Recurse -Force

    Write-Host "已打包   : $zip" -ForegroundColor Green
    Write-Host ("           共 {0} 个文件，自带 BepInEx {1}，解压到游戏根目录即可" -f $fileCount, $bepVer)
}
