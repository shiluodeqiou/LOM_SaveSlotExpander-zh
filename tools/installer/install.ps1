<#
    把本目录里的 mod 装进《活侠传》。

    可以从任意位置运行——哪怕解压到了下载文件夹也没关系，脚本自己会去找游戏。
    找不到时，把游戏文件夹直接拖到「点我安装.bat」上即可。
#>
[CmdletBinding()]
param([string]$GameRoot)

$ErrorActionPreference = "Stop"
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

$here   = $PSScriptRoot
$plugin = "LOM 存档位扩展（汉化版）"

function Say($text)  { Write-Host $text }
function Good($text) { Write-Host $text -ForegroundColor Green }
function Warn($text) { Write-Host $text -ForegroundColor Yellow }

# ---------------------------------------------------------------- 检查本目录

Say ""
Say "=== $plugin 安装程序 ==="
Say ""
Say "本目录: $here"
Say ""

$payload = @(
    "winhttp.dll",
    "doorstop_config.ini",
    "BepInEx\core\BepInEx.Unity.Mono.Preloader.dll",
    "BepInEx\plugins\LOM_SaveSlotExpander\LOM_SaveSlotExpander.dll"
)
$missing = @($payload | Where-Object { -not (Test-Path (Join-Path $here $_)) })
if ($missing.Count -gt 0) {
    Warn "这个文件夹里缺少下列文件，看起来压缩包没有解压完整："
    $missing | ForEach-Object { Say "    $_" }
    Say ""
    Say "压缩包里除了 BepInEx 文件夹，还有几个文件必须一起解压出来"
    Say "（winhttp.dll、doorstop_config.ini、.doorstop_version、changelog.txt）。"
    Say "少了它们，游戏启动时根本不会加载 BepInEx，表现就是「装了没反应」。"
    Say ""
    Say "请回到压缩包，重新完整解压一次，再运行本程序。"
    exit 1
}

# ---------------------------------------------------------------- 找游戏

function Test-GameRoot([string]$path) {
    if ([string]::IsNullOrWhiteSpace($path)) { return $false }
    # 兼容用户把 exe 或存档路径拖进来的情况
    if (Test-Path $path -PathType Leaf) { $path = Split-Path $path -Parent }
    return (Test-Path (Join-Path $path "Mortal_Data\Managed\Mortal.Core.dll"))
}

function Find-GameRoot {
    # 1) 本目录本身就是游戏目录
    if (Test-GameRoot $here) { return $here }

    # 2) 本目录的各级上级（解压到游戏目录里的子文件夹时会命中这里）
    $dir = $here
    for ($i = 0; $i -lt 4; $i++) {
        $dir = Split-Path $dir -Parent
        if ([string]::IsNullOrWhiteSpace($dir)) { break }
        if (Test-GameRoot $dir) { return $dir }
    }

    # 3) Steam 的各个库
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

    # 4) 常见位置逐个试
    foreach ($drive in (Get-PSDrive -PSProvider FileSystem).Name) {
        foreach ($suffix in @("SteamLibrary\steamapps\common\LegendOfMortal",
                              "Steam\steamapps\common\LegendOfMortal",
                              "Program Files (x86)\Steam\steamapps\common\LegendOfMortal",
                              "games\LegendOfMortal")) {
            $candidate = "${drive}:\$suffix"
            if (Test-GameRoot $candidate) { return $candidate }
        }
    }
    return $null
}

$target = $null
if ($GameRoot) {
    if (-not (Test-GameRoot $GameRoot)) {
        Warn "指定的位置不是《活侠传》的游戏目录：$GameRoot"
        Say "（里面应当有 Mortal.exe 和 Mortal_Data 文件夹）"
        exit 1
    }
    $target = $GameRoot
} else {
    $target = Find-GameRoot
}

if (-not $target) {
    Warn "没有找到《活侠传》的游戏目录。"
    Say ""
    Say "请这样做：把《活侠传》的游戏文件夹（Mortal.exe 所在的那个文件夹，"
    Say "也就是 Steam 库里「管理 → 浏览本地文件」打开的那个）直接拖到"
    Say "「点我安装.bat」上，再松手。"
    exit 1
}

if ($target -eq $here) {
    Good "本目录就是游戏目录，文件已经在正确的位置上了。"
    Say ""
    Say "接下来启动游戏即可。进游戏后："
    Say "    打开存档或读档界面 → 把鼠标移到存档位上 → 按 F2 写注释"
    Say "    按 F3 切换排序（按编号 / 按存档时间）"
    exit 0
}

# ---------------------------------------------------------------- 安装

Say "游戏目录: $target"
Say ""
Say "正在复制文件……"

$copied = 0
try {
    # BepInEx 运行时（只覆盖 BepInEx 自己的文件，不碰 config 和别人的插件）
    foreach ($f in @("winhttp.dll", "doorstop_config.ini", ".doorstop_version", "changelog.txt")) {
        $src = Join-Path $here $f
        if (Test-Path $src) { Copy-Item $src -Destination $target -Force; $copied++ }
    }

    $coreSrc = Join-Path $here "BepInEx\core"
    $coreDst = Join-Path $target "BepInEx\core"
    if (-not (Test-Path $coreDst)) { New-Item -ItemType Directory -Path $coreDst -Force | Out-Null }
    foreach ($f in (Get-ChildItem $coreSrc -File)) {
        Copy-Item $f.FullName -Destination $coreDst -Force
        $copied++
    }

    # 本 mod 本体
    $pluginSrc = Join-Path $here "BepInEx\plugins\LOM_SaveSlotExpander"
    $pluginDst = Join-Path $target "BepInEx\plugins\LOM_SaveSlotExpander"
    if (-not (Test-Path $pluginDst)) { New-Item -ItemType Directory -Path $pluginDst -Force | Out-Null }
    foreach ($f in (Get-ChildItem $pluginSrc -File -Recurse)) {
        $rel = $f.FullName.Substring($pluginSrc.Length).TrimStart('\')
        $out = Join-Path $pluginDst $rel
        $outDir = Split-Path $out -Parent
        if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }
        Copy-Item $f.FullName -Destination $out -Force
        $copied++
    }
}
catch {
    Warn "复制过程中出错：$($_.Exception.Message)"
    Say ""
    Say "如果游戏装在 C:\Program Files (x86)\ 下面，可能是权限不足。"
    Say "请右键「点我安装.bat」→「以管理员身份运行」再试一次。"
    exit 1
}

# ---------------------------------------------------------------- 验证

$need = @(
    "winhttp.dll",
    "doorstop_config.ini",
    "BepInEx\core\BepInEx.Unity.Mono.Preloader.dll",
    "BepInEx\plugins\LOM_SaveSlotExpander\LOM_SaveSlotExpander.dll"
)
$bad = @($need | Where-Object { -not (Test-Path (Join-Path $target $_)) })

Say ""
Say "已复制 $copied 个文件。"
if ($bad.Count -eq 0) {
    Good "安装完成，检查通过。"
    Say ""
    Say "接下来启动游戏即可。进游戏后："
    Say "    打开存档或读档界面，把鼠标移到（或用光标选中）一个存档位上"
    Say "    F2  ——  写 / 修改这个存档位的注释（Enter 确定 / Esc 取消）"
    Say "    F3  ——  切换排序：按存档位编号 ↔ 按存档时间从新到旧"
    Say ""
    Say "第一次启动后，设置文件会生成在 BepInEx\config\lom.zh.saveslotexpander.cfg。"
    Say "想确认装没装上，可以看 BepInEx\LogOutput.log 里有没有「读档界面实际生成存档位 100 个」。"
} else {
    Warn "这些文件没能就位，安装可能没有成功："
    $bad | ForEach-Object { Say "    $_" }
    exit 1
}
