# 局部修复已弃用：请改用全量脚本（会重写引用）
Write-Host "Use Tools/FixAllCorruptedGuids.ps1 instead (full remap + reference rewrite)."
& "$PSScriptRoot\FixAllCorruptedGuids.ps1"
