# Gera o .aab assinado para a Play Store (Builds/TrafficRush.aab).
# Uso (Unity fechada, na raiz do projeto):  & .\Tools\build-release.ps1 [-VersionCode 2]
# A senha da keystore é pedida aqui e só existe enquanto o script roda; nada é salvo em disco.
param([int]$VersionCode = 1)

$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'
$project = Split-Path -Parent $PSScriptRoot
$keystore = 'C:\projects\trafficrush.keystore'

if (-not (Test-Path $keystore)) { Write-Host "Keystore nao encontrada em $keystore. Crie primeiro com o keytool." -ForegroundColor Red; exit 1 }

$secure = Read-Host -AsSecureString 'Senha da keystore'
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
    $env:TRAFFICRUSH_KEYSTORE_PASS = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    Write-Host "Gerando o .aab (versionCode $VersionCode). Leva uns 5-10 minutos..." -ForegroundColor Cyan
    $p = Start-Process -FilePath $unity -Wait -PassThru -ArgumentList @(
        '-batchmode', '-quit', '-buildTarget', 'Android',
        '-projectPath', "`"$project`"",
        '-executeMethod', 'AndroidBuild.BuildRelease',
        '-versionCode', $VersionCode,
        '-logFile', "`"$project\Logs\release.log`"")
}
finally {
    Remove-Item Env:\TRAFFICRUSH_KEYSTORE_PASS -ErrorAction SilentlyContinue
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
}

if ($p.ExitCode -eq 0 -and (Test-Path "$project\Builds\TrafficRush.aab")) {
    Write-Host "PRONTO: $project\Builds\TrafficRush.aab" -ForegroundColor Green
} else {
    Write-Host "FALHOU (codigo $($p.ExitCode)). Veja Logs\release.log" -ForegroundColor Red
}
