$ErrorActionPreference = "Stop"

$cargoBin = Join-Path $env:USERPROFILE ".cargo\bin"
if (Test-Path $cargoBin) {
  $env:Path = "$cargoBin;$env:Path"
}

function Test-Command {
  param(
    [Parameter(Mandatory = $true)]
    [string]$Name
  )

  $command = Get-Command $Name -ErrorAction SilentlyContinue
  if ($null -eq $command) {
    Write-Host "[missing] $Name"
    return $false
  }

  Write-Host "[ok] $Name -> $($command.Source)"
  return $true
}

function Test-VsBuildTools {
  $vswhere = "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"
  if (-not (Test-Path $vswhere)) {
    Write-Host "[missing] Visual Studio Installer / vswhere"
    return $false
  }

  $installation = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
  if ([string]::IsNullOrWhiteSpace($installation)) {
    Write-Host "[missing] Visual Studio C++ Build Tools"
    return $false
  }

  Write-Host "[ok] Visual Studio C++ Build Tools -> $installation"
  return $true
}

function Test-WebView2Runtime {
  $webView2Guid = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
  $paths = @(
    "HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\$webView2Guid",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\$webView2Guid"
  )

  foreach ($path in $paths) {
    if (Test-Path $path) {
      $version = (Get-ItemProperty $path).pv
      Write-Host "[ok] WebView2 Runtime -> $version"
      return $true
    }
  }

  Write-Host "[missing] WebView2 Runtime"
  return $false
}

$results = @(
  (Test-Command "git"),
  (Test-Command "rustup"),
  (Test-Command "cargo"),
  (Test-Command "node"),
  (Test-Command "pnpm"),
  (Test-VsBuildTools)
)

$tauriEnabled = $args -contains "--with-tauri"
if ($tauriEnabled) {
  $results += Test-WebView2Runtime
}

try {
  $printers = Get-Printer | Select-Object -First 5 Name, DriverName
  if ($printers) {
    Write-Host "[info] Printers detected:"
    $printers | Format-Table | Out-String | Write-Host
  }
} catch {
  Write-Host "[warn] Unable to enumerate printers: $($_.Exception.Message)"
}

if ($results -contains $false) {
  Write-Error "Bootstrap verification failed."
}

Write-Host "Bootstrap verification passed."
