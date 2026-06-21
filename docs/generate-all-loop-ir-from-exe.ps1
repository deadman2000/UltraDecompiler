$OutputDir = "docs\loops-ir-graphs"

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

function Gen-IR-FromSource {
    param(
        [string]$Source,
        [string]$Name,
        [string]$Offset,
        [string]$Opt
    )

    $dotFile = Join-Path $OutputDir "$Name`_$Opt.dot"
    $pngFile = Join-Path $OutputDir "$Name`_$Opt.png"

    Write-Host "  $Name /$Opt ($Offset)..."

    & dotnet run --project Tools -- ir-graph `
        --source $Source `
        --offset $Offset `
        --opt $Opt `
        --out $dotFile `
        --png 2>&1 | Out-Null

    if (Test-Path $pngFile) {
        return $true
    }

    Write-Warning "PNG not created: $pngFile"
    return $false
}

# Name — подпись в Loops.md; Offset — смещение начала функции в EXE
$loopCases = @(
    @{
        Source = "forlp.c"
        Functions = @(
            @{ Name = "sum_for"; Offset = "0x10"; Opt = "Od" },
            @{ Name = "countdown"; Offset = "0x45"; Opt = "Od" },
            @{ Name = "sum_for"; Offset = "0x10"; Opt = "Ox" },
            @{ Name = "countdown"; Offset = "0x36"; Opt = "Ox" }
        )
    },
    @{
        Source = "loopspec.c"
        Functions = @(
            @{ Name = "for_step3"; Offset = "0x10"; Opt = "Od" },
            @{ Name = "for_mul"; Offset = "0x45"; Opt = "Od" },
            @{ Name = "for_no_update"; Offset = "0x81"; Opt = "Od" },
            @{ Name = "for_multi_var"; Offset = "0xB6"; Opt = "Od" },
            @{ Name = "for_var_step"; Offset = "0x108"; Opt = "Od" },
            @{ Name = "while_pre"; Offset = "0x144"; Opt = "Od" },
            @{ Name = "do_while"; Offset = "0x179"; Opt = "Od" },
            @{ Name = "while_break"; Offset = "0x1AB"; Opt = "Od" },
            @{ Name = "for_empty"; Offset = "0x1DB"; Opt = "Od" },
            @{ Name = "nested_for"; Offset = "0x205"; Opt = "Od" },
            @{ Name = "for_step3"; Offset = "0x10"; Opt = "Ox" },
            @{ Name = "for_mul"; Offset = "0x42"; Opt = "Ox" },
            @{ Name = "for_no_update"; Offset = "0x7A"; Opt = "Ox" },
            @{ Name = "for_multi_var"; Offset = "0xAA"; Opt = "Ox" },
            @{ Name = "for_var_step"; Offset = "0xF0"; Opt = "Ox" },
            @{ Name = "while_pre"; Offset = "0x128"; Opt = "Ox" },
            @{ Name = "do_while"; Offset = "0x15A"; Opt = "Ox" },
            @{ Name = "while_break"; Offset = "0x188"; Opt = "Ox" },
            @{ Name = "for_empty"; Offset = "0x1AE"; Opt = "Ox" },
            @{ Name = "nested_for"; Offset = "0x1CC"; Opt = "Ox" },
            @{ Name = "for_break"; Offset = "0x250"; Opt = "Od" },
            @{ Name = "for_continue"; Offset = "0x2A0"; Opt = "Od" },
            @{ Name = "while_break"; Offset = "0x2F0"; Opt = "Od" },
            @{ Name = "while_continue"; Offset = "0x340"; Opt = "Od" },
            @{ Name = "for_break"; Offset = "0x200"; Opt = "Ox" },
            @{ Name = "for_continue"; Offset = "0x240"; Opt = "Ox" },
            @{ Name = "while_break"; Offset = "0x280"; Opt = "Ox" },
            @{ Name = "while_continue"; Offset = "0x2C0"; Opt = "Ox" }
        )
    },
    @{
        Source = "strcp.c"
        Functions = @(
            @{ Name = "ptr_loop"; Offset = "0x10"; Opt = "Od" },
            @{ Name = "ptr_loop"; Offset = "0x10"; Opt = "Ox" }
        )
    }
)

$ok = 0
$fail = 0

foreach ($case in $loopCases) {
    Write-Host "`n=== $($case.Source) ==="
    foreach ($func in $case.Functions) {
        if (Gen-IR-FromSource -Source $case.Source -Name $func.Name -Offset $func.Offset -Opt $func.Opt) {
            $ok++
        } else {
            $fail++
        }
    }
}

Write-Host ""
Write-Host "Done: $ok ok, $fail failed."
if ($fail -gt 0) {
    exit 1
}
