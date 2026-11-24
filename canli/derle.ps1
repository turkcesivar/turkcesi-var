# Canlı Çeviri: canli.exe'yi derler, canli\cikti\ klasörüne kopyalar ve testleri çalıştırır.
# Windows PowerShell 5.1 ile uyumludur.
$ErrorActionPreference = 'Stop'
$kok = $PSScriptRoot
$cikti = Join-Path $kok 'cikti'

dotnet build (Join-Path $kok 'Canli\Canli.csproj') -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$derlenen = Join-Path $kok 'Canli\bin\Release\net48'
if (Test-Path $cikti) { Remove-Item $cikti -Recurse -Force }
New-Item -ItemType Directory -Path $cikti | Out-Null
Copy-Item (Join-Path $derlenen '*') $cikti -Recurse -Force

dotnet test (Join-Path $kok 'Canli.Testler')
exit $LASTEXITCODE
