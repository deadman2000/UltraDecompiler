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
        $Source `
        -o $Offset `
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
    # --- forlp.c /Od ---
    @{ Source = "forlp.c"; Opt = "Od"; Functions = @(
        @{ Name = "sum_for"; Offset = "0x10" },
        @{ Name = "countdown"; Offset = "0x45" }
    )},

    # --- forlp.c /Ox ---
    @{ Source = "forlp.c"; Opt = "Ox"; Functions = @(
        @{ Name = "sum_for"; Offset = "0x10" },
        @{ Name = "countdown"; Offset = "0x36" }
    )},

    # --- loopspec.c /Od ---
    @{ Source = "loopspec.c"; Opt = "Od"; Functions = @(
        @{ Name = "for_step3"; Offset = "0x10" },
        @{ Name = "for_mul"; Offset = "0x45" },
        @{ Name = "for_no_update"; Offset = "0x81" },
        @{ Name = "for_multi_var"; Offset = "0xB6" },
        @{ Name = "for_var_step"; Offset = "0x108" },
        @{ Name = "while_pre"; Offset = "0x144" },
        @{ Name = "do_while"; Offset = "0x179" },
        @{ Name = "while_break"; Offset = "0x1AB" },
        @{ Name = "for_empty"; Offset = "0x1DB" },
        @{ Name = "nested_for"; Offset = "0x205" },
        @{ Name = "for_break"; Offset = "0x252" },
        @{ Name = "for_continue"; Offset = "0x295" },
        @{ Name = "while_break"; Offset = "0x2DD" },
        @{ Name = "while_continue"; Offset = "0x31A" }
    )},

    # --- loopspec.c /Ox ---
    @{ Source = "loopspec.c"; Opt = "Ox"; Functions = @(
        @{ Name = "for_step3"; Offset = "0x10" },
        @{ Name = "for_mul"; Offset = "0x42" },
        @{ Name = "for_no_update"; Offset = "0x7A" },
        @{ Name = "for_multi_var"; Offset = "0xAA" },
        @{ Name = "for_var_step"; Offset = "0xF0" },
        @{ Name = "while_pre"; Offset = "0x128" },
        @{ Name = "do_while"; Offset = "0x15A" },
        @{ Name = "while_break"; Offset = "0x188" },
        @{ Name = "for_empty"; Offset = "0x1AE" },
        @{ Name = "nested_for"; Offset = "0x1CC" },
        @{ Name = "for_break"; Offset = "0x208" },
        @{ Name = "while_break"; Offset = "0x242" },
        @{ Name = "for_continue"; Offset = "0x280" },
        @{ Name = "while_continue"; Offset = "0x2BA" }
    )},

    # --- strcp.c /Od ---
    @{ Source = "strcp.c"; Opt = "Od"; Functions = @(
        @{ Name = "ptr_loop"; Offset = "0x10" }
    )},

    # --- strcp.c /Ox ---
    @{ Source = "strcp.c"; Opt = "Ox"; Functions = @(
        @{ Name = "ptr_loop"; Offset = "0x10" }
    )}
)

$ok = 0
$fail = 0

foreach ($case in $loopCases) {
    Write-Host "`n=== $($case.Source) /$($case.Opt) ==="
    foreach ($func in $case.Functions) {
        if (Gen-IR-FromSource -Source $case.Source -Name $func.Name -Offset $func.Offset -Opt $case.Opt) {
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
