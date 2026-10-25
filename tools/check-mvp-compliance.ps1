# MVP Design Rules Compliance Checker
# Checks all sample code against the 14 MVP design rules

param(
    [string]$SamplesPath = "src\Kibinago.Samples",
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  MVP Design Rules Compliance Checker" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$script:violations = @()
$script:warnings = @()
$script:successes = 0

function Add-Violation {
    param([string]$File, [int]$Line, [string]$Rule, [string]$Message)
    $script:violations += [PSCustomObject]@{
        File = $File
        Line = $Line
        Rule = $Rule
        Message = $Message
    }
}

function Add-Warning {
    param([string]$File, [int]$Line, [string]$Rule, [string]$Message)
    $script:warnings += [PSCustomObject]@{
        File = $File
        Line = $Line
        Rule = $Rule
        Message = $Message
    }
}

function Add-Success {
    $script:successes++
}

# Rule 1: View naming convention
function Test-Rule1 {
    Write-Host "[Rule 1] Checking View naming conventions..." -ForegroundColor Yellow

    $viewInterfaces = Get-ChildItem -Path $SamplesPath -Filter "I*View.cs" -Recurse
    foreach ($file in $viewInterfaces) {
        $content = Get-Content $file.FullName -Raw

        # Check interface naming
        if ($content -match "public\s+interface\s+I(\w+)\s*:") {
            $interfaceName = $matches[1]
            if (-not $interfaceName.EndsWith("View")) {
                Add-Violation -File $file.Name -Line 0 -Rule "Rule 1" `
                    -Message "Interface name 'I$interfaceName' should end with 'View'"
            } else {
                Add-Success
            }
        }
    }

    $viewClasses = Get-ChildItem -Path $SamplesPath -Filter "*Form.cs" -Recurse
    foreach ($file in $viewClasses) {
        $content = Get-Content $file.FullName -Raw

        # Check if it implements a View interface
        if ($content -match "class\s+(\w+)\s*:\s*Form\s*,\s*I(\w+)View") {
            Add-Success
        }
    }
}

# Rule 2: Presenter naming convention
function Test-Rule2 {
    Write-Host "[Rule 2] Checking Presenter naming conventions..." -ForegroundColor Yellow

    $presenterFiles = Get-ChildItem -Path $SamplesPath -Filter "*Presenter.cs" -Recurse
    foreach ($file in $presenterFiles) {
        $content = Get-Content $file.FullName -Raw

        if ($content -match "public\s+class\s+(\w+)\s*:") {
            $className = $matches[1]
            if (-not $className.EndsWith("Presenter")) {
                Add-Violation -File $file.Name -Line 0 -Rule "Rule 2" `
                    -Message "Class name '$className' should end with 'Presenter'"
            } else {
                Add-Success
            }
        }
    }
}

# Rule 3: Responsibility separation
function Test-Rule3 {
    Write-Host "[Rule 3] Checking responsibility separation..." -ForegroundColor Yellow

    $presenterFiles = Get-ChildItem -Path $SamplesPath -Filter "*Presenter.cs" -Recurse
    foreach ($file in $presenterFiles) {
        $lines = Get-Content $file.FullName
        $lineNum = 0

        foreach ($line in $lines) {
            $lineNum++

            # Check for direct UI manipulation
            if ($line -match "\.(BackColor|ForeColor|Location|Size|Visible)\s*=") {
                Add-Violation -File $file.Name -Line $lineNum -Rule "Rule 3" `
                    -Message "Presenter should not manipulate UI properties directly"
            }

            # Check for control-specific code
            if ($line -match "new\s+(Button|TextBox|Label|ListBox|DataGridView)") {
                Add-Violation -File $file.Name -Line $lineNum -Rule "Rule 3" `
                    -Message "Presenter should not create UI controls"
            }
        }
    }
}

# Rule 4: OnXxx() naming for event handlers
function Test-Rule4 {
    Write-Host "[Rule 4] Checking OnXxx() naming for event handlers..." -ForegroundColor Yellow

    $presenterFiles = Get-ChildItem -Path $SamplesPath -Filter "*Presenter.cs" -Recurse
    foreach ($file in $presenterFiles) {
        $lines = Get-Content $file.FullName
        $lineNum = 0

        foreach ($line in $lines) {
            $lineNum++

            # Check for private/protected void methods that don't start with On
            if ($line -match "(private|protected)\s+void\s+(\w+)\s*\(") {
                $methodName = $matches[2]
                if (-not $methodName.StartsWith("On") -and $methodName -notmatch "^(Initialize|Dispose|Validate|Register|Load|Save|Update|Delete|Create|Handle)") {
                    Add-Warning -File $file.Name -Line $lineNum -Rule "Rule 4" `
                        -Message "Method '$methodName' might be an event handler and should start with 'On'"
                } elseif ($methodName.StartsWith("On")) {
                    Add-Success
                }
            }
        }
    }
}

# Rule 6: No return values from Presenter methods called by View
function Test-Rule6 {
    Write-Host "[Rule 6] Checking for forbidden return values..." -ForegroundColor Yellow

    $presenterFiles = Get-ChildItem -Path $SamplesPath -Filter "*Presenter.cs" -Recurse
    foreach ($file in $presenterFiles) {
        $lines = Get-Content $file.FullName
        $lineNum = 0

        foreach ($line in $lines) {
            $lineNum++

            # Check for public methods with return values
            if ($line -match "public\s+(\w+)\s+On(\w+)\s*\(" -and $matches[1] -ne "void") {
                Add-Violation -File $file.Name -Line $lineNum -Rule "Rule 6" `
                    -Message "Public On* method should return void (Tell, Don't Ask principle)"
            }
        }
    }
}

# Rule 7: Access View only through interface
function Test-Rule7 {
    Write-Host "[Rule 7] Checking View access through interface..." -ForegroundColor Yellow

    $presenterFiles = Get-ChildItem -Path $SamplesPath -Filter "*Presenter.cs" -Recurse
    foreach ($file in $presenterFiles) {
        $content = Get-Content $file.FullName -Raw

        # Check for concrete Form type references
        if ($content -match "private\s+\w+Form\s+") {
            Add-Violation -File $file.Name -Line 0 -Rule "Rule 7" `
                -Message "Presenter should not reference concrete Form types"
        }

        # Check for casts to Form
        if ($content -match "as\s+Form\s*\)") {
            Add-Warning -File $file.Name -Line 0 -Rule "Rule 7" `
                -Message "Casting View to Form type - consider adding method to interface instead"
        }
    }
}

# Rule 10: Long meaningful names
function Test-Rule10 {
    Write-Host "[Rule 10] Checking for meaningful method names..." -ForegroundColor Yellow

    $viewInterfaces = Get-ChildItem -Path $SamplesPath -Filter "I*View.cs" -Recurse
    foreach ($file in $viewInterfaces) {
        $lines = Get-Content $file.FullName
        $lineNum = 0

        foreach ($line in $lines) {
            $lineNum++

            # Check for generic method names
            if ($line -match "void\s+(Set|Get|Update|Show|Hide)\s*\(") {
                $methodName = $matches[1]
                Add-Warning -File $file.Name -Line $lineNum -Rule "Rule 10" `
                    -Message "Method name '$methodName' is too generic - use domain-specific naming"
            }

            # Check for long meaningful names
            if ($line -match "void\s+(\w{20,})\s*\(") {
                Add-Success
            }
        }
    }
}

# Rule 11: Prefer methods over properties
function Test-Rule11 {
    Write-Host "[Rule 11] Checking interface design (methods vs properties)..." -ForegroundColor Yellow

    $viewInterfaces = Get-ChildItem -Path $SamplesPath -Filter "I*View.cs" -Recurse
    foreach ($file in $viewInterfaces) {
        $lines = Get-Content $file.FullName
        $lineNum = 0

        foreach ($line in $lines) {
            $lineNum++

            # Check for bidirectional properties (get; set;)
            if ($line -match "\s+\w+\s+\w+\s*\{\s*get;\s*set;\s*\}") {
                Add-Warning -File $file.Name -Line $lineNum -Rule "Rule 11" `
                    -Message "Bidirectional property violates Tell Don't Ask - consider setter-only or method"
            }

            # Accept setter-only properties
            if ($line -match "\s+\w+\s+\w+\s*\{\s*set;\s*\}") {
                Add-Success
            }
        }
    }
}

# Rule 13: No UI control names in interface methods
function Test-Rule13 {
    Write-Host "[Rule 13] Checking for UI control references in interface..." -ForegroundColor Yellow

    $viewInterfaces = Get-ChildItem -Path $SamplesPath -Filter "I*View.cs" -Recurse
    foreach ($file in $viewInterfaces) {
        $lines = Get-Content $file.FullName
        $lineNum = 0

        foreach ($line in $lines) {
            $lineNum++

            # Check for control type names in method names
            $controlTypes = @("Button", "TextBox", "Label", "ListBox", "DataGrid", "TreeView", "TabControl", "Panel")
            foreach ($controlType in $controlTypes) {
                if ($line -match "void\s+\w*$controlType\w*\s*\(") {
                    Add-Violation -File $file.Name -Line $lineNum -Rule "Rule 13" `
                        -Message "Method name contains UI control type '$controlType' - use domain naming"
                }
            }
        }
    }
}

# Rule 14: Domain naming
function Test-Rule14 {
    Write-Host "[Rule 14] Checking for domain-driven naming..." -ForegroundColor Yellow

    $viewInterfaces = Get-ChildItem -Path $SamplesPath -Filter "I*View.cs" -Recurse
    foreach ($file in $viewInterfaces) {
        $content = Get-Content $file.FullName -Raw

        # Look for domain-specific method names (positive check)
        if ($content -match "void\s+(Display|Show|Highlight|Mark|Enable|Add|Remove|Update|Clear)\w+\s*\(") {
            Add-Success
        }
    }
}

# Run all tests
Test-Rule1
Test-Rule2
Test-Rule3
Test-Rule4
Test-Rule6
Test-Rule7
Test-Rule10
Test-Rule11
Test-Rule13
Test-Rule14

# Generate report
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Compliance Report" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Successes: $($script:successes)" -ForegroundColor Green
Write-Host "Warnings:  $($script:warnings.Count)" -ForegroundColor Yellow
Write-Host "Violations: $($script:violations.Count)" -ForegroundColor Red
Write-Host ""

if ($script:violations.Count -gt 0) {
    Write-Host "VIOLATIONS:" -ForegroundColor Red
    Write-Host "============" -ForegroundColor Red
    foreach ($v in $script:violations) {
        Write-Host "  [$($v.Rule)] $($v.File):$($v.Line)" -ForegroundColor Red
        Write-Host "    $($v.Message)" -ForegroundColor Gray
    }
    Write-Host ""
}

if ($Verbose -and $script:warnings.Count -gt 0) {
    Write-Host "WARNINGS:" -ForegroundColor Yellow
    Write-Host "=========" -ForegroundColor Yellow
    foreach ($w in $script:warnings) {
        Write-Host "  [$($w.Rule)] $($w.File):$($w.Line)" -ForegroundColor Yellow
        Write-Host "    $($w.Message)" -ForegroundColor Gray
    }
    Write-Host ""
}

# Summary
if ($script:violations.Count -eq 0) {
    Write-Host "✅ All samples comply with MVP design rules!" -ForegroundColor Green
} else {
    Write-Host "❌ Found $($script:violations.Count) violation(s) - please review" -ForegroundColor Red
}

Write-Host ""

# Export detailed report
$reportPath = "mvp-compliance-report.json"
$report = @{
    Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Successes = $script:successes
    Warnings = $script:warnings
    Violations = $script:violations
}
$report | ConvertTo-Json -Depth 10 | Out-File $reportPath
Write-Host "Detailed report exported to: $reportPath" -ForegroundColor Cyan
