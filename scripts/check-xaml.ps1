[CmdletBinding()]
param(
    [string]$ProjectRoot
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Split-Path -Parent $PSScriptRoot
}
$xamlFiles = Get-ChildItem -Path (Join-Path $ProjectRoot "src\GameSaveCenter.Playnite") -Recurse -Filter *.xaml
$errors = [System.Collections.Generic.List[string]]::new()

foreach ($file in $xamlFiles) {
    $document = [System.Xml.XmlDocument]::new()
    $document.PreserveWhitespace = $true

    try {
        $document.Load($file.FullName)
    }
    catch {
        $errors.Add("$($file.FullName): XML parsing failed: $($_.Exception.Message)")
        continue
    }

    foreach ($triggerProperty in @("DataTemplate.Triggers", "ControlTemplate.Triggers", "Style.Triggers")) {
        $expectedParent = $triggerProperty.Split('.')[0]
        $nodes = $document.SelectNodes("//*[local-name()='$triggerProperty']")
        foreach ($node in $nodes) {
            if ($null -eq $node.ParentNode -or $node.ParentNode.LocalName -ne $expectedParent) {
                $actualParent = if ($null -eq $node.ParentNode) { "<none>" } else { $node.ParentNode.LocalName }
                $errors.Add("$($file.FullName): <$triggerProperty> must be a direct child of <$expectedParent>; actual parent: <$actualParent>.")
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
                    $errors.Add("$($file.FullName): <$templateName> TargetName='$targetName' does not reference a named element.")
                    continue
                }

                if ($namedElements[$targetName] -match "Transform$") {
                    $errors.Add("$($file.FullName): TargetName='$targetName' references $($namedElements[$targetName]); target a visual element and set its RenderTransform instead.")
                }
            }
        }
    }


    $styleTransformSetters = $document.SelectNodes("//*[local-name()='Style']/*[local-name()='Setter' and @Property='RenderTransform']/*[local-name()='Setter.Value']/*[substring(local-name(), string-length(local-name()) - string-length('Transform') + 1) = 'Transform']")
    foreach ($transform in $styleTransformSetters) {
        $errors.Add("$($file.FullName): <$($transform.LocalName)> in a Style Setter can be shared and frozen by WPF; animated controls need an independent mutable transform.")
    }

    $codeBehind = "$($file.FullName).cs"
    if (Test-Path $codeBehind) {
        $codeText = Get-Content -Raw -Path $codeBehind
        $handlerNames = [System.Collections.Generic.HashSet[string]]::new()
        foreach ($element in $document.SelectNodes("//*")) {
            foreach ($attribute in $element.Attributes) {
                if ($attribute.Value -match '^On[A-Za-z0-9_]+$') {
                    [void]$handlerNames.Add($attribute.Value)
                }
            }
        }
        foreach ($handlerName in $handlerNames) {
            if ($codeText -notmatch ("\b" + [Regex]::Escape($handlerName) + "\s*\(")) {
                $errors.Add("$($file.FullName): XAML event handler '$handlerName' is not defined in $codeBehind.")
            }
        }
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    throw "XAML structural validation failed with $($errors.Count) error(s)."
}

Write-Host "XAML structural validation passed for $($xamlFiles.Count) file(s)."
