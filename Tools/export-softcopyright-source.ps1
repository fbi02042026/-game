# 软著源程序鉴别材料导出（与 Assets/Editor/SoftCopyrightSourceExport.cs 同逻辑）
# 用法: powershell -File Tools/export-softcopyright-source.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path (Join-Path $root "Assets"))) { throw "Cannot locate project root from $PSScriptRoot" }

$LinesPerPage = 50
$PagesEach = 30
$PreferredOrder = @(
    "Assets/Scripts/Core/SaveData.cs",
    "Assets/Scripts/Systems/MercenaryOfferGenerator.cs",
    "Assets/Scripts/UI/TavernRosterPanel.cs",
    "Assets/Scripts/UI/TownHeroCostumePreview.cs",
    "Assets/Scripts/Unit/HeroCostumeManager.cs",
    "Assets/Scripts/Systems/MercenaryManager.cs",
    "Assets/Scripts/Config/SkillRegistry.cs",
    "Assets/Scripts/Systems/StageRoller.cs",
    "Assets/Scripts/UI/RestStagePopupUI.cs",
    "Assets/Scripts/Core/GameConfig.cs",
    "Assets/Scripts/Core/GameAudio.cs",
    "Assets/Scripts/Core/GameBgm.cs",
    "Assets/Scripts/Story/TutorialDirector.cs",
    "Assets/Scripts/UI/BattleHeadTalkUI.cs",
    "Assets/Scripts/Systems/SaveSystem.cs",
    "Assets/Scripts/Systems/ResourceWallet.cs",
    "Assets/Scripts/Systems/StaminaSystem.cs",
    "Assets/Scripts/Systems/StageClearRewardDirector.cs",
    "Assets/Scripts/Systems/PreLevelSystem.cs",
    "Assets/Scripts/Systems/OfflineGoldCalc.cs",
    "Assets/Scripts/Systems/TownSaveAlign.cs",
    "Assets/Scripts/Managers/BattleManager.cs",
    "Assets/Scripts/Managers/ChapterManager.cs",
    "Assets/Scripts/UI/TownHubController.cs",
    "Assets/Scripts/UI/AdventureUI.cs",
    "Assets/Scripts/UI/GuildHallUI.cs",
    "Assets/Scripts/UI/AdventureLogUI.cs",
    "Assets/Scripts/UI/CharacterUI.cs",
    "Assets/Scripts/UI/BattleStageMapUI.cs",
    "Assets/Scripts/UI/OfflineRewardPopup.cs",
    "Assets/Scripts/Combat/DamageFormula.cs",
    "Assets/Scripts/Platform/CloudSaveBridge.cs",
    "Assets/Scripts/Platform/RewardedAdBridge.cs"
)

$allLines = New-Object System.Collections.Generic.List[string]
$used = New-Object System.Collections.Generic.HashSet[string]

foreach ($rel in $PreferredOrder) {
    $full = Join-Path $root ($rel -replace '/', [IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path $full)) { continue }
    [void]$allLines.Add("// ===== FILE: $rel =====")
    Get-Content $full -Encoding UTF8 | ForEach-Object { [void]$allLines.Add($_) }
    [void]$allLines.Add("")
    [void]$used.Add($rel)
}

$scriptsDir = Join-Path $root "Assets/Scripts"
$need = $PagesEach * 2 * $LinesPerPage
if (Test-Path $scriptsDir) {
    Get-ChildItem $scriptsDir -Filter *.cs -Recurse | Sort-Object FullName | ForEach-Object {
        if ($allLines.Count -ge $need) { return }
        $rel = $_.FullName.Substring($root.Length).TrimStart('\','/') -replace '\\','/'
        if ($used.Contains($rel)) { return }
        [void]$allLines.Add("// ===== FILE: $rel =====")
        Get-Content $_.FullName -Encoding UTF8 | ForEach-Object { [void]$allLines.Add($_) }
        [void]$allLines.Add("")
        [void]$used.Add($rel)
    }
}

$outDir = Join-Path $root "Docs/软著源码鉴别"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function Write-Paged($outPath, $start, $count, $tag) {
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("======== 源程序鉴别材料 · ${tag}30页 · 每页${LinesPerPage}行 ========")
    [void]$sb.AppendLine("")
    $end = [Math]::Min($allLines.Count, $start + $count)
    $page = 1
    $lineInPage = 0
    for ($i = $start; $i -lt $end; $i++) {
        if ($lineInPage -eq 0) { [void]$sb.AppendLine("---------- 第 $page 页 ----------") }
        [void]$sb.AppendLine($allLines[$i])
        $lineInPage++
        if ($lineInPage -ge $LinesPerPage) {
            $lineInPage = 0
            $page++
            [void]$sb.AppendLine("")
        }
    }
    if ($lineInPage -gt 0) { [void]$sb.AppendLine("") }
    [IO.File]::WriteAllText($outPath, $sb.ToString(), [Text.UTF8Encoding]::new($false))
}

$needLines = $PagesEach * $LinesPerPage
Write-Paged (Join-Path $outDir "源程序_前30页.txt") 0 $needLines "前"
$backStart = [Math]::Max(0, $allLines.Count - $needLines)
Write-Paged (Join-Path $outDir "源程序_后30页.txt") $backStart $needLines "后"

$note = @"
导出时间: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
登记版本: V0.3.5（最新）
每页行数: $LinesPerPage
前后页数: $PagesEach
总拼接行数: $($allLines.Count)
文件数: $($used.Count)
优先收录: SaveData / MercenaryOfferGenerator / GameAudio / GameBgm / TutorialDirector / BattleHeadTalkUI 等
用法: 将 txt 按页排版为 PDF（≥50 行/页）提交鉴别材料。
也可在团结编辑器执行 Tools/软著/导出源程序鉴别材料 覆盖本目录。
提交记录: git commit 含「软著 V0.3.5」时与本版代码一致。
"@
[IO.File]::WriteAllText((Join-Path $outDir "导出说明.txt"), $note, [Text.Encoding]::UTF8)
Write-Host "Exported to $outDir ($($allLines.Count) lines, $($used.Count) files)"
