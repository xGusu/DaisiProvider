# Signs a DaisiProvider publish folder with the self-signed code-signing
# certificate (see new-certificate.ps1) and checks the result.
# Signs DaisiProvider.Desktop.exe and the own DaisiProvider.*.dll files;
# Microsoft runtime files are already signed by Microsoft.
#   powershell -NoProfile -ExecutionPolicy Bypass -File assets\signing\sign-publish.ps1 -Package runtime
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('runtime', 'selfcontained')]
    [string]$Package
)
$ErrorActionPreference = 'Stop'

$subject = 'CN=Gustav Vanderdi'
$timestampServer = 'http://timestamp.digicert.com'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')

# Folder name follows the publish profiles: publish\DaisiProvider-<version>-<package>.
$props = Get-Content (Join-Path $repoRoot 'Directory.Build.props') -Raw
if ($props -notmatch '<Version>([^<]+)</Version>') { throw 'No <Version> in Directory.Build.props.' }
$folder = Join-Path $repoRoot "publish\DaisiProvider-$($Matches[1])-$Package"
if (-not (Test-Path $folder)) { throw "Publish folder not found: $folder" }

$certificate = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
    Where-Object { $_.Subject -eq $subject -and $_.NotAfter -gt (Get-Date) -and $_.HasPrivateKey } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1
if (-not $certificate) {
    throw "No valid code-signing certificate '$subject' with private key on this PC. Run assets\signing\new-certificate.ps1 first."
}

$files = @(Get-ChildItem $folder -File | Where-Object { $_.Name -eq 'DaisiProvider.Desktop.exe' -or $_.Name -like 'DaisiProvider.*.dll' })
if ($files.Count -eq 0) { throw "Nothing to sign in $folder" }

# The timestamp keeps the signature valid after the certificate expires. If the
# server cannot be reached (offline, company proxy), sign without it and say so.
$useTimestamp = $true
foreach ($file in $files) {
    $result = $null
    if ($useTimestamp) {
        try {
            $result = Set-AuthenticodeSignature -FilePath $file.FullName -Certificate $certificate -HashAlgorithm SHA256 -TimestampServer $timestampServer
        }
        catch {
            $result = $null
        }
        if (-not $result -or -not $result.SignerCertificate -or -not $result.TimeStamperCertificate) {
            Write-Output "Timestamp server not reachable; signing without timestamp."
            $useTimestamp = $false
            $result = $null
        }
    }
    if (-not $result) {
        $result = Set-AuthenticodeSignature -FilePath $file.FullName -Certificate $certificate -HashAlgorithm SHA256
    }
}

# Check every file: signed with this certificate. Valid = trusted on this PC.
$failed = 0
foreach ($file in $files) {
    $signature = Get-AuthenticodeSignature -FilePath $file.FullName
    $ours = $signature.SignerCertificate -and $signature.SignerCertificate.Thumbprint -eq $certificate.Thumbprint
    $stamp = if ($signature.TimeStamperCertificate) { 'timestamped' } else { 'no timestamp' }
    Write-Output ("{0}: {1}, {2}" -f $file.Name, $signature.Status, $stamp)
    if (-not $ours) { $failed++ }
}
if ($failed -gt 0) { throw "$failed file(s) not signed with $subject." }
Write-Output "Signed $($files.Count) file(s) in $folder with $subject ($($certificate.Thumbprint))."
