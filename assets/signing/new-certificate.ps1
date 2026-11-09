# Creates the self-signed code-signing certificate for DaisiProvider in the
# current user's certificate store, exports its public part next to this
# script and trusts it for the current user.
# The private key stays in the Windows store of this PC; it never goes into
# the repository. Run once per PC that publishes:
#   powershell -NoProfile -ExecutionPolicy Bypass -File assets\signing\new-certificate.ps1
# Windows shows one security dialog for the Trusted Root step; answer Yes.
$ErrorActionPreference = 'Stop'

$subject = 'CN=Gustav Vanderdi'
$cerPath = Join-Path $PSScriptRoot 'DaisiProvider-codesign.cer'

$certificate = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
    Where-Object { $_.Subject -eq $subject -and $_.NotAfter -gt (Get-Date) -and $_.HasPrivateKey } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if ($certificate) {
    Write-Output "Certificate exists: $($certificate.Thumbprint), valid until $($certificate.NotAfter.ToString('yyyy-MM-dd'))"
}
else {
    $certificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $subject `
        -KeyAlgorithm RSA `
        -KeyLength 3072 `
        -HashAlgorithm SHA256 `
        -KeyExportPolicy NonExportable `
        -NotAfter (Get-Date).AddYears(5) `
        -CertStoreLocation Cert:\CurrentUser\My
    Write-Output "Certificate created: $($certificate.Thumbprint), valid until $($certificate.NotAfter.ToString('yyyy-MM-dd'))"
}

# Public part only: lets other PCs trust signed files. Contains no private key.
Export-Certificate -Cert $certificate -FilePath $cerPath -Type CERT | Out-Null
Write-Output "Public certificate exported: $cerPath"

# Trust for this user: Root makes the chain valid, TrustedPublisher names the publisher.
foreach ($store in 'Root', 'TrustedPublisher') {
    $known = Get-ChildItem "Cert:\CurrentUser\$store" | Where-Object { $_.Thumbprint -eq $certificate.Thumbprint }
    if ($known) {
        Write-Output "Already trusted in CurrentUser\$store"
    }
    else {
        Import-Certificate -FilePath $cerPath -CertStoreLocation "Cert:\CurrentUser\$store" | Out-Null
        Write-Output "Trusted in CurrentUser\$store"
    }
}
