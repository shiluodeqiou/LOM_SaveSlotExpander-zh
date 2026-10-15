# LOM 存档位扩展（汉化版）

**把《活侠传》（Legend of Mortal）的存档位从 20 个扩到最多 999 个，并且能给每个存档位写注释的 BepInEx 插件。**

完全不改动存档文件格式，卸载 MOD 后游戏照常能玩。

> 本工程是原作者 **ame-chan** 的 [LOM_SaveSlotExpander](https://gitlab.com/ame-chan/lom_saveslotexpander)（面向日语环境）的**汉化版**。
> 界面文字、日志、设置说明全部改为简体中文，字体策略也改成面向中文玩家（详见[与原版的差异](#与原版的差异)）。

## 截图

读档界面——每个存档位下面就是自己写的注释，只显示在有存档的格子上：

![读档界面与存档注释](展示图片/存档备注.png)

存档位可以一直排到 999，列表也能正常滚到最后：

![100 个存档位](展示图片/100个存档.png)

---

## 目录

- [截图](#截图)
- [能做什么](#能做什么)
- [运行环境](#运行环境)
- [安装](#安装)
- [用法](#用法)
- [设置](#设置)
- [原理](#原理)
- [注意事项](#注意事项)
- [游戏更新之后](#游戏更新之后)
- [构建](#构建)
- [测试与核对](#测试与核对)
- [卸载](#卸载)
- [与原版的差异](#与原版的差异)
- [许可](#许可)

---

## 能做什么

| 功能 | 说明 |
| --- | --- |
| **存档位扩展** | 把存档界面、读档界面的存档位数量改成设置值（默认 100） |
| **注释** | 给每个存档位写任意文字，显示在列表的存档位旁边。支持中文输入法 |
| **排序** | 按存档时间从新到旧排列。一个按键就能切回原本的编号顺序 |
| **列表加速** | 存档位变多也不会卡住界面，显示用的元数据会缓存起来 |

---

## 运行环境

| 项目 | 内容 |
| --- | --- |
| 游戏 | 活侠传 / Legend of Mortal（已在 **v1.0.6000.2** 上核对通过） |
| MOD 加载器 | **BepInEx 6（Unity Mono 版）** |
| Unity | 2020.3.x |

BepInEx 5 不能用。`BepInEx\core\BepInEx.Unity.Mono.dll` 存在就是 6 系。

**不需要**安装日语化 MOD，也不需要任何其它本地化 MOD。字体直接用游戏自带的，
缺字时会自动退回系统里的中文字体。

### 版本号

版本号按**发布日期**取，格式为 `年.月.日`（例如 `2026.10.16`），Git 标签为 `v2026.10.16`。
发布当天又改的话，就在同一天里重新发一版，日期不变。

本汉化版基于**原版 v1.2.0**，功能与原版一致，差异见[与原版的差异](#与原版的差异)。

---

## 安装

1. 从 [Releases](https://github.com/shiluodeqiou/LOM_SaveSlotExpander-zh/releases) 下载最新版的 zip
2. 把压缩包里的 `BepInEx` 文件夹**解压到游戏根目录**（`Mortal.exe` 所在的那个文件夹），
   与原文件夹合并即可 —— 压缩包里已经带好了目录结构，不需要自己去找 `BepInEx\plugins\`

   游戏根目录在哪：在 Steam 库里右键《活侠传》→ 管理 → 浏览本地文件。

   装好后应当是这个位置：

```
LegendOfMortal/
└── BepInEx/
    └── plugins/
        └── LOM_SaveSlotExpander/
            └── LOM_SaveSlotExpander.dll
```

3. 启动游戏（首次启动会生成设置文件）

压缩包里还附了一份 `安装说明.txt`，同样的内容。

> **需要 BepInEx 6（Unity Mono 版）。** 如果游戏根目录下没有 `BepInEx` 文件夹，
> 说明还没装 BepInEx，请先去它的发布页装好 6.x 的 Unity Mono 版本。
>
> 不要和**原版**（`lom.jp.saveslotexpander`）同时安装，两边会重复打补丁。

自行构建的话：`build.ps1 -Deploy` 会直接装进游戏，`build.ps1 -Package` 会生成同样结构的发布包。

---

## 用法

### 存档位

打开存档／读档界面，就会按设置的数量排出存档位。操作方式跟原来一样。

### 注释

1. 在存档界面或读档界面，把**鼠标移到**目标存档位上，或**用光标选中**它
2. 按 **F2**（可用 `CommentHotkey` 改键）
3. 弹出输入框，输入文字
4. **Enter** 确定 / **Esc** 取消

注释只会显示在有存档的格子上，位置是日期时间的下面一行，且是一条**独立的行**。
不会碰标题（第○年○月○旬），所以注释写得再长，日期的字号也不会被缩小。

注释本身是 ini 文件，也可以用文本编辑器在游戏外直接改。

### 排序

在存档／读档界面按 **F3**，排列顺序就会切换（可用 `SortToggleHotkey` 改键）。

| 排列 | 说明 |
| --- | --- |
| **按存档位编号** | 游戏本来的顺序（001、002、003……） |
| **按存档时间** | 越新保存的越靠上 |

选「按存档时间」时，两个界面的排法略有不同。

- **读档界面** —— 最上面是最近保存的那一档，往下依次变旧
- **存档界面** —— 最上面是新建存档用的空位，第二个才是最近保存的那一档

两种排法都会把空位集中放在后面，并按编号排序。
选择的排列会保存下来，下次启动还是这个排法。

**重排不会改变存档位编号与存档数据的对应关系。**
变的只是显示顺序，017 号存档重排之后仍然是 017 号。

---

## 设置

全部是 ini 格式的文本文件，可以用文本编辑器随意修改。

### 存档位数量与行为

`BepInEx\config\lom.zh.saveslotexpander.cfg`（首次启动时生成）

| 项目 | 默认值 | 说明 |
| --- | --- | --- |
| `SlotCount` | `100` | **存档位数量（2～999）**。想显示多少就改这个 |
| `EnableMetaCache` | `true` | 列表显示加速。一般保持开启 |
| `EnableComments` | `true` | 注释功能 |
| `CommentHotkey` | `F2` | 打开注释编辑框的按键。填 `UnityEngine.InputSystem.Key` 的名字（`F4`、`Insert`、`Backquote` 等） |
| `CommentLabelFormat` | `【{0}】` | 注释的显示格式，`{0}` 是注释正文 |
| `CommentMaxLength` | `40` | 注释最多多少个字 |
| `DeleteCommentWithSave` | `true` | 删除存档时一并删掉注释 |
| `CommentFontSize` | `0` | 注释字号。`0` 表示跟日期时间一样大 |
| `CommentOffsetX` | `0` | 注释位置横向微调（像素，右为正） |
| `CommentOffsetY` | `0` | 注释位置纵向微调（像素，上为正） |
| `CommentWidth` | `0` | 换行宽度（像素）。`0` 表示跟标题栏对齐 |
| `CommentMaxLines` | `1` | 注释占几行。填 `2` 以上会压到下面的存档位 |
| `CommentMinFontSize` | `12` | 缩小时的字号下限 |
| `CommentBottomPadding` | `0` | 列表末尾追加的留白。`0` 表示行高的一半 |
| `CommentFontMode` | `Auto` | 注释字体。`Auto`＝游戏自带字体，缺字才自动换；`GameFont`＝始终用游戏字体；`Custom`＝用 `CommentFontName` 指定的字体 |
| `CommentFontName` | （空） | 注释用的字体名（部分匹配即可）。留空则按上面的模式自动决定 |
| `SortMode` | `Default` | 排列顺序。`Default` 按编号，`RecentFirst` 按存档时间从新到旧 |
| `SortToggleHotkey` | `F3` | 切换排列的按键。只在存档／读档界面里有效 |

改完数字后重启游戏生效。

> 配置文件的**节名和键名**与原版完全一致（`1.General`、`2.Comment`……），
> 所以从原版迁移时，可以直接把旧配置里的内容抄过来。

### 注释内容

`SaveSlotComments.ini`（与存档数据同一个文件夹）

```ini
001 = 一周目·善人路线
017 = 打 BOSS 前
042 = 隐藏事件已回收
```

格式是「存档位编号 = 注释」。请在游戏退出后修改
（游戏运行中改了会被覆盖）。

### 生成的文件

都会放在存档数据所在的文件夹
（`%USERPROFILE%\AppData\LocalLow\Obb Studio\Mortal\<SteamID>\`）。

| 文件 | 用途 |
| --- | --- |
| `SaveSlotComments.ini` | 每个存档位的注释 |
| `SaveSlotExpander_meta.txt` | 列表显示用的元数据缓存（删掉会自动重建） |

---

## 原理

### 为什么能突破 20 个的限制

游戏本体的 `Mortal.Core.SaveSystem` 就是以 `Save_<存档位名>.dat` 保存的，
**保存逻辑本身没有数量限制**。存档位名是三位零填充的字符串，所以 `001`～`999` 都能直接用。

20 这个上限的真正来源是 `SaveGamePanel._slotCount` / `LoadGamePanel._slotCount`
（都是 `[SerializeField] int`，默认 20）——**它只是界面上排多少个格子**。
本 MOD 就是用 Harmony 把这个值改掉而已。

### 缺字与字形

本作内嵌的字库是思源宋体 TC（繁体字库）。简体字绝大多数都在里面，
但个别生僻字可能缺。所以本 MOD 在画注释前会用 `Font.HasCharacter`
逐字检查，遇到画不出来的字才换一个能画的字体。

默认（`CommentFontMode = Auto`）会优先使用**游戏界面正在用的字体**，
这样注释的字形和界面完全一致。只有游戏字库缺字时才依次尝试：
设置里指定的字体 → 已加载的中文字体 → 系统里的中文字体。

> 如果你装了替换字体的本地化 MOD，本 MOD 借到的就是那个字体，不需要额外设置。

### 注释为什么单独放一个文件

存档本体是用 `BinaryFormatter` 序列化的，往 `GameSave` 里加字段会让
已有存档读不出来。所以注释不混进存档本体，而是另存一个文件。

正因为这样设计，**卸载 MOD 后游戏也不会坏**。
（卸载后 021 之后的存档位不再显示，但文件都还在）

### 列表为什么能变快

游戏原本的列表显示，会为每个存档位把 60KB 以上的存档本体
用 `BinaryFormatter` 整个还原一遍。20 个格子没问题，
300 个格子就是每次打开界面都卡好几秒。

所以本 MOD **只在列表显示期间**，把存档数据换成只带
「年月·时间·体验版标记·周目标记」的轻量替身。
这份信息会拿文件的修改时刻和大小做校验后缓存，第二次以后就不必再读磁盘。
真正的读取流程没有任何改动，读进去的内容跟以前一模一样。

---

## 注意事项

- **第一次会慢一点** —— 要为已有存档逐个建立元数据，第一次打开列表会花些时间。第二次起就快了。
- **Steam 云** —— 一个存档约 65KB，100 个约 7MB，可能接近云存储的容量上限。另外 `SaveSlotComments.ini` 若不在同步范围内，注释不会带到另一台电脑（存档本体可以）。
- **调小 `SlotCount` 时** —— 减掉的那些存档文件不会被删除，只是不再显示。调回去就又能看到。
- **自动存档位**（`auto` / `auto_free` / `auto_battle`）不在处理范围内。仍然保持原来的 3 个，也不能写注释。

---

## 游戏更新之后

本 MOD 是改游戏本体来运作的，所以**游戏更新后可能会失效**。
这里说明那时会发生什么、该怎么办。

### 最重要的一点：存档不会丢

**本 MOD 并没有用什么特殊的自有格式在存档。**
第 21 个之后的存档，也是用游戏原本的格式、原本的位置、原本的命名方式保存的。
MOD 只是把「界面上排多少个格子」变多了而已。

所以就算 MOD 失效了，存档文件仍然原样留在电脑里。
**不是消失了，只是暂时不显示出来。**

### 会发生什么

**情况一：官方自己加了存档位**

最好的情况。你用 MOD 建的第 21 个之后的存档，很可能直接出现在官方界面上。
也许就不再需要这个 MOD 了。

**情况二：MOD 失效了**

存档界面回到原本的 20 个格子，第 21 个之后看不到。
再说一遍：数据还在。

### 怎么把看不到的数据找回来

按顺序往下试。

**办法一：等 MOD 更新**

最简单。跟着游戏改动把 MOD 修好，就又能全部看到了。
「把格子数变多」这个机制本身很简单，重新做出来的把握很大。

**办法二：最近在玩的那一档可以直接继续**

游戏记得「最后在哪个格子玩」。比如你一直在第 57 格玩，
就算 MOD 没运行，也能从标题界面的「最近的存档」直接读进去继续。

**办法三：改文件名，挪到能看见的格子里**

就算 MOD 一直修不好，用这个办法一定能拿回来。

存档数据放在这里：

```
C:\Users\（你的用户名）\AppData\LocalLow\Obb Studio\Mortal\（一串数字的文件夹）
```

在资源管理器的地址栏输入 `%USERPROFILE%\AppData\LocalLow\Obb Studio\Mortal`
就能打开。里面有 `Save_001.dat`、`Save_002.dat`…… 排着。
**这个编号就是游戏界面上显示的格子编号。**

比如想把第 57 格的数据在第 5 格里读出来：

1. 退出游戏
2. 把 `Save_005.dat` 改名成 `Save_005_备份.dat` 之类挪开
   （第 5 格里有好东西时这么做；本来是空的就不用管）
3. 把 `Save_057.dat` 改名成 `Save_005.dat`
4. 启动游戏 → 第 5 格里出现第 57 格的数据

虽然一次只能救 20 格，重复这个步骤就能把需要的数据陆续救出来。

> **动手之前把整个文件夹复制一份备份，会安心很多。**
> 名字改错了也能还原。

### 千万不要做的事

在第 21 格之后看不到的状态下，**不要在显示「没有数据」的格子上开始新游戏。**
那个格子会被新数据覆盖。

另外，只在 1～20 格里正常玩的话，第 21 格之后不会有任何变化，可以放心。

### 注释会怎么样

注释跟存档数据分开，存在另一个文件（`SaveSlotComments.ini`）里，
和格子编号一起保存。MOD 修好后就会原样显示出来。
内容就是普通文字，用记事本打开也能看。

---

## 构建

`dist/` 里已经有成品，只是使用的话不需要构建。

### 有 .NET SDK 时

```powershell
dotnet build src/LOM_SaveSlotExpander.csproj -c Release -p:GameRoot="D:\Steam\steamapps\common\LegendOfMortal"
```

### 没有 SDK 时（有 Visual Studio / Build Tools 就行）

`build.ps1` 会自行判断有没有 SDK，没有就直接调用 Visual Studio 自带的 Roslyn。

```powershell
pwsh -File build.ps1
```

游戏位置会自动查找（读 Steam 的库信息）。
找不到或想用别的位置时，明确指定即可。

```powershell
pwsh -File build.ps1 -GameRoot "D:\Steam\steamapps\common\LegendOfMortal"
```

加 `-Deploy` 会在构建后自动复制到 `BepInEx\plugins\`。

```powershell
pwsh -File build.ps1 -Deploy
```

加 `-Verify` 会在构建后核对补丁目标是否还认得当前版本的游戏（见下一节）。

```powershell
pwsh -File build.ps1 -Deploy -Verify
```

输出位置是 `dist/LOM_SaveSlotExpander/LOM_SaveSlotExpander.dll`。

---

## 测试与核对

### 保存格式的往返测试

这套测试既不依赖游戏也不依赖 Unity，没有游戏的环境也能跑。

```powershell
pwsh -File tests/run.ps1
```

有 .NET SDK 的话这样也行。

```bash
dotnet run --project tests/Tests.csproj
```

### 补丁目标核对（游戏更新后先跑这个）

`tools/PatchCheck` 会静态地把本 MOD 打补丁的目标、取用的私有字段和方法签名，
逐条对到当前版本的游戏程序集上。**不需要启动游戏**，所以游戏更新后可以先跑它，
马上知道 MOD 还认不认得游戏内部结构。

```powershell
dotnet run --project tools/PatchCheck -- "D:\Steam\steamapps\common\LegendOfMortal"
```

全部 OK 就说明补丁能挂上。出现「缺失」就是游戏内部结构变了，需要跟着改。

还可以列出游戏里某个类型的成员，方便人工对照。

```powershell
dotnet run --project tools/PatchCheck -- "D:\Steam\steamapps\common\LegendOfMortal" --list LoadSlotPanel
```

### 运行时怎么看

启动游戏后看 `BepInEx\LogOutput.log`。加载成功时会是这样：

```
[Info   :   BepInEx] Loading [LOM 存档位扩展（汉化版） 2026.10.16]
[Info   :LOM 存档位扩展（汉化版）] LOM 存档位扩展（汉化版） 2026.10.16 加载完成（基于原版 1.2.0）(存档位数=100, 元数据缓存=True, 注释=True)
[Info   :LOM 存档位扩展（汉化版）] 存档文件夹: C:\Users\...\AppData\LocalLow\Obb Studio\Mortal\7656119xxxxxxxxxx
[Info   :LOM 存档位扩展（汉化版）] 读档界面实际生成存档位 100 个
```

**`实际生成存档位 N 个` 这一行最值得看。** 设置里填的数字和游戏真正排出来的格子数
不一定一致（游戏更新改了内部结构就会不一致），所以补丁生效时会额外报出实际数量。
这个数字跟设置对得上，就说明扩展功能正常。

游戏更新后再启动一次，如果这一行不见了、或者数字变成了 20，就说明补丁失效了，
先用上面的 `tools/PatchCheck` 核对一下。

---

## 发布新版本

版本号跟着**发布日期**走，流程如下。

1. 改代码后，把 `src/Plugin.cs` 里的 `VERSION` 和 `src/LOM_SaveSlotExpander.csproj` 里的
   `<Version>` 都改成当天的日期（例如 `2026.10.16`），并在 `CHANGELOG.md` 里加上
   对应标题（`## v2026.10.16`）和内容
2. 构建、核对、部署、打包一次过：

   ```powershell
   pwsh -File build.ps1 -Deploy -Verify -Package
   ```

3. 最好照[运行时怎么看](#运行时怎么看)启动一次游戏，确认「实际生成存档位 N 个」对得上
4. 提交并推送，然后打同名标签

   ```bash
   git add -A
   git commit -m "……"
   git push origin main
   git tag -a v2026.10.16 -m "v2026.10.16"
   git push origin v2026.10.16
   ```

5. 在 GitHub 的 [Releases](https://github.com/shiluodeqiou/LOM_SaveSlotExpander-zh/releases)
   里以该标签新建 Release，正文从 `CHANGELOG.md` 摘取，附件上传 `dist/` 下生成的
   `LOM_SaveSlotExpander-zh-v<版本>.zip`

**压缩包的目录结构由 `-Package` 负责**，里面是 `BepInEx\plugins\LOM_SaveSlotExpander\`
再加上一份 `安装说明.txt`。用户解压到游戏根目录就装好了，不需要自己找 `BepInEx\plugins\`。
打包时版本号从 `src/Plugin.cs` 里读，所以不会和代码里的版本对不上。

同一天里又改的话，日期不变，重新发一版即可。

---

## 卸载

删掉 `BepInEx\plugins\LOM_SaveSlotExpander\` 文件夹即可，不影响存档数据。

连注释一起清掉的话，再删掉存档文件夹里的
`SaveSlotComments.ini` 和 `SaveSlotExpander_meta.txt`。

---

## 与原版的差异

汉化版基于原版 **v1.2.0**，改动如下。

| 项目 | 原版 | 汉化版 |
| --- | --- | --- |
| 插件 GUID | `lom.jp.saveslotexpander` | `lom.zh.saveslotexpander` |
| 设置文件名 | `lom.jp.saveslotexpander.cfg` | `lom.zh.saveslotexpander.cfg` |
| 界面文字 | 日语 | **简体中文** |
| 日志、设置说明 | 日语 | **简体中文** |
| 注释字体 | `Auto` 时检测日语化 MOD，有就用日语字体 | `Auto` 时用游戏自带字体，缺字才退回中文字体 |
| `CommentFontMode` 取值 | `Auto` / `Japanese` / `GameFont` | `Auto` / `GameFont` / `Custom` |
| 日语化 MOD | 需要检测（`JapaneseModDetector`） | **已移除**，不需要也不检测 |
| 回退字体 | 日语字体（Noto Serif JP、游明朝等） | **中文字体**（思源宋体、微软雅黑、宋体等） |
| 附送工具 | 无 | `tools/PatchCheck` 补丁目标核对工具 |

**功能、文件格式、配置的节名与键名都保持不变**，
所以原版的用户可以直接把旧配置和注释文件拿来继续用。

移除 `JapaneseModDetector` 的原因：原版靠「有没有装日语化 MOD」来决定注释用不用日语字体，
是为了解决日语玩家看到的汉字字形是中文的问题。对中文玩家来说，
游戏自带字体的字形本来就是对的，这个判断没有意义。
而且如果玩家真装了替换字体的 MOD，本 MOD 会直接从游戏界面借字体，
本来就能自动用上那个字体，无需另外检测。

---

## 许可

[MIT License](LICENSE)（继承自原版）
