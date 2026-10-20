# function Get-SolutionInventory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SolutionPath
    )

    $ProjectTypes = @{
        '{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}' = 'C# (.NET Framework)'
        '{9A19103F-16F7-4668-BE54-9A1E7A4F7556}' = 'C# SDK-style (.NET Core/.NET 5+)'
        '{F184B08F-C81C-45F6-A57F-5ABD9991F28F}' = 'VB.NET'
        '{F2A71F9B-5D33-465A-A702-920D77279786}' = 'F#'
        '{6EC3EE1D-3C4E-46DD-8F32-0CC8E7565705}' = 'F# SDK-style'
        '{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}' = 'C++'

        '{8BB2217D-0F2D-49D1-97BC-3654ED321F3B}' = 'ASP.NET 5'
        '{603C0E0B-DB56-11DC-BE95-000D561079B0}' = 'ASP.NET MVC 1'
        '{F85E285D-A4E0-4152-9332-AB1D724D3325}' = 'ASP.NET MVC 2'
        '{E53F8FEA-EAE0-44A6-8774-FFD645390401}' = 'ASP.NET MVC 3'
        '{E3E379DF-F4C6-4180-9B81-6769533ABE47}' = 'ASP.NET MVC 4'
        '{349C5851-65DF-11DA-9384-00065B846F21}' = 'ASP.NET MVC 5 / Web Application'

        '{356CAE8B-CFD3-4221-B0A8-081A261C0C10}' = 'ASP.NET Core Empty'
        '{687AD6DE-2DF8-4B75-A007-DEF66CD68131}' = 'ASP.NET Core Web API'
        '{E27D8B1D-37A3-4EFC-AFAE-77744ED86BCA}' = 'ASP.NET Core Web App'
        '{065C0379-B32B-4E17-B529-0A722277FE2D}' = 'ASP.NET Core MVC'
        '{32F807D6-6071-4239-8605-A9B2205AAD60}' = 'ASP.NET Core Angular'
        '{4C3A4DF3-0AAD-4113-8201-4EEEA5A70EED}' = 'ASP.NET Core React'

        '{30E03E5A-5F87-4398-9D0D-FEB397AFC92D}' = 'Azure Functions'
        '{C8A4CD56-20F4-440B-8375-78386A4431B9}' = 'Blazor Server'

        '{2EFF6E4D-FF75-4ADF-A9BE-74BEC0B0AFF8}' = 'Class Library'
        '{008A663C-3F22-40EF-81B0-012B6C27E2FB}' = 'Console Application'

        '{A9ACE9BB-CECE-4E62-9AA4-C7E7C5BD2124}' = 'Database Project'
        '{00D1A9C2-B5F0-4AF3-8072-F6C62B433612}' = 'SQL Database Project'

        '{66A26720-8FB5-11D2-AA7E-00C04F688DDE}' = 'Solution Folder'
    }

    $RedFlagProjectVersions = @{
        '8.00' = $true
        '9.00' = $true
        '10.00' = $true
    }

    $content = Get-Content $SolutionPath

    # Solution metadata
    $formatVersion = $null
    $vsVersion = $null

    $projects = @()
    $insideSection = $false

    foreach ($line in $content) {

        if ($line -match '^Microsoft Visual Studio Solution File,\s*Format Version\s*(.+)$') {
            $formatVersion = $matches[1].Trim()
            continue
        }

        if ($line -match '^# Visual Studio.*?(\d+)') {
            $vsVersion = $matches[1]
            continue
        }

        if ($line -match '^Project\("(?<TypeGuid>{[^"]+})"\)\s*=\s*"(?<Name>[^"]+)",\s*"(?<Path>[^"]+)",\s*"(?<ProjectGuid>{[^"]+})"') {
            Write-Output "Found project: $($matches.Name)"
            $projects += [PSCustomObject]@{
                Name         = $matches.Name
                Path         = $matches.Path
                TypeName     = $ProjectTypes[$matches.TypeGuid]
                TypeGuid     = $matches.TypeGuid
            }
        }
    }

    [PSCustomObject]@{
        Solution        = (Split-Path $SolutionPath -Leaf)
        SolutionPath    = $SolutionPath
        FormatVersion   = $formatVersion
        RedFlagProjectVersion = $RedFlagProjectVersions[$formatVersion]
        VisualStudioVersion   = $vsVersion
        ProjectCount    = $projects.Count
        Projects        = $projects
    }

    Write-Output "Finished processing solution: $SolutionPath"