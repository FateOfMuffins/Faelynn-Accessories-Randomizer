[CmdletBinding()]
param(
    [string]$OutputPath,
    [switch]$Test
)

$ErrorActionPreference = 'Stop'
if (-not $OutputPath) { $OutputPath = Join-Path $PSScriptRoot 'dist\Faelynn-Accessories-Randomizer-Setup.exe' }
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
$framework = Split-Path -Parent $compiler
$arguments = @(
    '/nologo', '/target:winexe', '/optimize+', '/warnaserror+', "/out:$out",
    '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', '/reference:Microsoft.CSharp.dll',
    "/reference:$framework\WPF\PresentationFramework.dll", "/reference:$framework\WPF\PresentationCore.dll",
    "/reference:$framework\WPF\WindowsBase.dll", "/reference:$framework\System.Xaml.dll",
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
    Write-Output 'Passed: roll mapping, timers, setup layout and Close, autosave, and isolated install/uninstall.'
}
Write-Output $out
