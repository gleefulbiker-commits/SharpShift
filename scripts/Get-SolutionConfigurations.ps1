function Get-SolutionConfigurations {

    param(
        [string]$SolutionPath
    )

    $insideSection = $false

    Get-Content $SolutionPath | ForEach-Object {

        if ($_ -match 'GlobalSection\(SolutionConfigurationPlatforms\)') {
            $insideSection = $true
            return
        }

        if ($insideSection -and $_ -match 'EndGlobalSection') {
            $insideSection = $false
            return
        }

        if ($insideSection -and $_ -match '^\s*(.+?)\s*=\s*(.+)$') {

            [PSCustomObject]@{
                Configuration = $matches[1]
                Value = $matches[2]
            }
        }
    }
}