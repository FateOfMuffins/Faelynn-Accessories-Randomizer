[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot 'dist\Faelynn-Accessories-Randomizer-Setup.exe'),
    [switch]$Test
)

$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The Windows .NET Framework C# compiler was not found.'
}

$out = [IO.Path]::GetFullPath($OutputPath)
[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($out))
$sources = Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File
$arguments = @(
    '/nologo', '/target:winexe', '/optimize+', '/warnaserror+', "/out:$out",
    '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', '/reference:Microsoft.CSharp.dll',
    "/resource:$PSScriptRoot\GF2-Accessories.png,GF2-Accessories.png",
    "/resource:$PSScriptRoot\GF2-Splash.png,GF2-Splash.png"
)
$arguments += @($sources.FullName)
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }

if ($Test) {
    $scratch = Join-Path $PSScriptRoot ('dist\self-test-' + [Guid]::NewGuid().ToString('N'))
    $process = Start-Process -FilePath $out -ArgumentList ('--self-test "{0}"' -f $scratch) -PassThru -Wait -WindowStyle Hidden
    if ($process.ExitCode -ne 0) {
        $failure = Join-Path $scratch 'failure.txt'
        if (Test-Path -LiteralPath $failure) { Get-Content -LiteralPath $failure }
        throw "Self-test failed (exit $($process.ExitCode))."
    }
    Write-Output 'Passed: all 27 grid outcomes, default and custom launch delays, both transparent art surfaces, and install/save/uninstall in an isolated directory.'
}
Write-Output $out
