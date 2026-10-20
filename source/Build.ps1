param([switch]$Run, [switch]$Test)
$ErrorActionPreference = 'Stop'
$packageRoot = Split-Path -Parent $PSScriptRoot
$buildDir = Join-Path $packageRoot 'build'
$appPath = Join-Path $packageRoot 'FocusTime.exe'
$pendingPath = Join-Path $buildDir 'FocusTime.pending.exe'
$buildMarker = Join-Path $buildDir 'verified-build.txt'
$testPath = Join-Path $buildDir 'FocusTime.Tests.exe'
$logPath = Join-Path $packageRoot 'build-log.txt'
try {
    New-Item -ItemType Directory -Force -Path $buildDir | Out-Null
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
    if (-not (Test-Path -LiteralPath (Join-Path $framework 'csc.exe'))) {
        $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
    }
    $compiler = Join-Path $framework 'csc.exe'
    if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4 compiler was not found. Enable .NET Framework 4.8 in Windows, then try again.' }
    $wpf = Join-Path $framework 'WPF'
    $core = Join-Path $PSScriptRoot 'Core.cs'
    $app = Join-Path $PSScriptRoot 'App.cs'
    $tests = Join-Path $PSScriptRoot 'Tests.cs'
    $manifest = Join-Path $PSScriptRoot 'app.manifest'
    $icon = Join-Path $PSScriptRoot 'focus.ico'
    $wallpaper = Join-Path $PSScriptRoot 'ocean.jpg'
    $sources = @($core, $app, $manifest, $tests, $icon, $wallpaper, $PSCommandPath)
    $newest = ($sources | Get-Item | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1).LastWriteTimeUtc
    $rebuild = (-not (Test-Path -LiteralPath $appPath)) -or (-not (Test-Path -LiteralPath $buildMarker))
    if (-not $rebuild) { $rebuild = (Get-Item -LiteralPath $appPath).LastWriteTimeUtc -lt $newest }
    if ($rebuild -or $Test) {
        ('Build started: ' + (Get-Date -Format o)) | Set-Content -LiteralPath $logPath -Encoding UTF8
        $common = @('/nologo', '/codepage:65001', '/langversion:5', '/optimize+', '/warn:4', '/platform:anycpu',
            ('/r:' + (Join-Path $framework 'System.dll')),
            ('/r:' + (Join-Path $framework 'System.Core.dll')),
            ('/r:' + (Join-Path $framework 'System.Runtime.Serialization.dll')),
            ('/r:' + (Join-Path $framework 'System.Xml.dll')))
        Write-Host 'Compiling FocusTime...'
        $appArgs = $common + @('/target:winexe', ('/out:' + $pendingPath), ('/win32manifest:' + $manifest), ('/win32icon:' + $icon),
            ('/resource:' + $wallpaper + ',FocusTime.Ocean.jpg'),
            ('/r:' + (Join-Path $framework 'System.Xaml.dll')),
            ('/r:' + (Join-Path $wpf 'WindowsBase.dll')),
            ('/r:' + (Join-Path $wpf 'PresentationCore.dll')),
            ('/r:' + (Join-Path $wpf 'PresentationFramework.dll')), $core, $app)
        & $compiler @appArgs 2>&1 | Tee-Object -FilePath $logPath -Append
        if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed. See build-log.txt.' }
        Write-Host 'Compiling timer tests...'
        $testArgs = $common + @('/target:exe', ('/out:' + $testPath), $core, $tests)
        & $compiler @testArgs 2>&1 | Tee-Object -FilePath $logPath -Append
        if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed. See build-log.txt.' }
        Write-Host 'Running timer and storage tests...'
        & $testPath 2>&1 | Tee-Object -FilePath $logPath -Append
        if ($LASTEXITCODE -ne 0) { throw 'Self-tests failed. The app has not been started. See build-log.txt.' }
        Move-Item -LiteralPath $pendingPath -Destination $appPath -Force
        (Get-Date -Format o) | Set-Content -LiteralPath $buildMarker -Encoding UTF8
        Write-Host 'Build and self-tests passed.' -ForegroundColor Green
    }
    if ($Test) {
        Write-Host 'Checking Windows UI and generating screenshots...'
        $uiCheck = Start-Process -FilePath $appPath -ArgumentList '--smoke' -PassThru -WindowStyle Hidden
        if (-not $uiCheck.WaitForExit(60000)) { throw 'UI check timed out. Close the test window and inspect verification/.' }
        if ($uiCheck.ExitCode -ne 0) { throw 'UI check failed. See verification/ and build-log.txt.' }
        Write-Host 'UI checks passed. Screenshots are in verification/.' -ForegroundColor Green
    }
    if ($Run) { Start-Process -FilePath $appPath | Out-Null }
    exit 0
}
catch {
    $message = $_.Exception.Message
    Write-Host ('ERROR: ' + $message) -ForegroundColor Red
    try { Add-Content -LiteralPath $logPath -Value $message -Encoding UTF8 } catch { }
    exit 1
}
