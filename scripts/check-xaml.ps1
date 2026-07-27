[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
$xamlFiles = Get-ChildItem -Path (Join-Path $ProjectRoot "src\GameSaveCenter.Playnite") -Recurse -Filter *.xaml
$errors = [System.Collections.Generic.List[string]]::new()

foreach ($file in $xamlFiles) {
    $document = [System.Xml.XmlDocument]::new()
    $document.PreserveWhitespace = $true

    try {
        $document.Load($file.FullName)
    }
    catch {
        $errors.Add("$($file.FullName): XML 解析失败：$($_.Exception.Message)")
        continue
    }

    foreach ($triggerProperty in @("DataTemplate.Triggers", "ControlTemplate.Triggers", "Style.Triggers")) {
        $expectedParent = $triggerProperty.Split('.')[0]
        $nodes = $document.SelectNodes("//*[local-name()='$triggerProperty']")
        foreach ($node in $nodes) {
            if ($null -eq $node.ParentNode -or $node.ParentNode.LocalName -ne $expectedParent) {
                $actualParent = if ($null -eq $node.ParentNode) { "<none>" } else { $node.ParentNode.LocalName }
                $errors.Add("$($file.FullName): <$triggerProperty> 必须直属 <$expectedParent>，当前父元素为 <$actualParent>。")
            }
        }
    }

    foreach ($templateName in @("ControlTemplate", "DataTemplate")) {
        $templates = $document.SelectNodes("//*[local-name()='$templateName']")
        foreach ($template in $templates) {
            $namedElements = @{}
            $nameNodes = $template.SelectNodes(".//*[@*[local-name()='Name']]")
            foreach ($nameNode in $nameNodes) {
                $nameAttribute = $nameNode.Attributes | Where-Object { $_.LocalName -eq "Name" } | Select-Object -First 1
                if ($null -ne $nameAttribute -and -not [string]::IsNullOrWhiteSpace($nameAttribute.Value)) {
                    $namedElements[$nameAttribute.Value] = $nameNode.LocalName
                }
            }

            $targetNodes = $template.SelectNodes(".//*[@TargetName]")
            foreach ($targetNode in $targetNodes) {
                $targetName = $targetNode.GetAttribute("TargetName")
                if (-not $namedElements.ContainsKey($targetName)) {
                    $errors.Add("$($file.FullName): <$templateName> 中 TargetName='$targetName' 找不到对应命名元素。")
                    continue
                }

                if ($namedElements[$targetName] -match "Transform$") {
                    $errors.Add("$($file.FullName): TargetName='$targetName' 指向 $($namedElements[$targetName])；模板触发器应定位可视元素，再整体设置 RenderTransform。")
                }
            }
        }
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    throw "XAML 结构检查失败，共 $($errors.Count) 项。"
}

Write-Host "XAML 结构检查通过，共检查 $($xamlFiles.Count) 个文件。"
