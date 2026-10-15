#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$MySqlBinPath = 'C:\Program Files\MySQL\MySQL Server 8.0\bin',
    [string]$ArtifactsPath = '',
    [ValidateSet('Identity', 'Onboarding', 'InstitutionCapacity', 'InstitutionSetup')][string]$Scenario = 'Identity',
    [ValidateRange(1, 100)][int]$ExpectedTestCount = 1
)

$ErrorActionPreference = 'Stop'
$serverPath = Join-Path $MySqlBinPath 'mysqld.exe'
$clientPath = Join-Path $MySqlBinPath 'mysql.exe'
foreach ($binaryPath in @($serverPath, $clientPath)) {
    if (-not (Test-Path -LiteralPath $binaryPath -PathType Leaf)) {
        throw 'MySQL Server 8 não encontrado. Indique a pasta bin com -MySqlBinPath.'
    }
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Instale o SDK .NET 10 antes de executar os testes.'
}

$repositoryPath = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $repositoryPath 'tests\SchoolGether.Api.IntegrationTests\SchoolGether.Api.IntegrationTests.csproj'
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$runId = [Guid]::NewGuid().ToString('N')
$runPath = Join-Path $temporaryRoot ('schoolgether-mysql-test-' + $runId)
$dataPath = Join-Path $runPath 'data'
$initPath = Join-Path $runPath 'init.sql'
$errorLog = Join-Path $runPath 'mysql-error.log'
$previousMySqlPassword = [Environment]::GetEnvironmentVariable('MYSQL_PWD', 'Process')
$previousTestConnection = [Environment]::GetEnvironmentVariable('SCHOOLGETHER_TEST_MYSQL', 'Process')
$serverProcess = $null
$serverReady = $false
$listener = $null
$testExitCode = 1

try {
    New-Item -ItemType Directory -Path $dataPath -Force | Out-Null
    $randomBytes = New-Object byte[] 32
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($randomBytes) } finally { $generator.Dispose() }
    $testPassword = [BitConverter]::ToString($randomBytes).Replace('-', '')
    $initSql = @"
ALTER USER 'root'@'localhost' IDENTIFIED BY '$testPassword';
CREATE DATABASE schoolgether_test_identity CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER 'schoolgether_test'@'localhost' IDENTIFIED BY '$testPassword';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, DROP, INDEX, REFERENCES ON schoolgether_test_identity.* TO 'schoolgether_test'@'localhost';
"@
    [IO.File]::WriteAllText($initPath, $initSql, (New-Object Text.UTF8Encoding($false)))
    Write-Host 'A preparar uma instância MySQL temporária, separada do serviço instalado...'
    & $serverPath --no-defaults --initialize-insecure "--datadir=$dataPath" "--log-error=$errorLog"
    if ($LASTEXITCODE -ne 0) { throw 'Falhou a inicialização do MySQL temporário.' }

    $listener = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $testPort = $listener.LocalEndpoint.Port
    $listener.Stop()
    $listener = $null
    $serverArguments = @('--no-defaults', "--datadir=`"$dataPath`"", '--bind-address=127.0.0.1',
        "--port=$testPort", '--mysqlx=OFF', "--init-file=`"$initPath`"", "--log-error=`"$errorLog`"",
        "--pid-file=`"$(Join-Path $runPath 'mysql.pid')`"")
    $serverProcess = Start-Process -FilePath $serverPath -ArgumentList $serverArguments -WindowStyle Hidden -PassThru
    $env:MYSQL_PWD = $testPassword
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        if ($serverProcess.HasExited) { throw 'O servidor MySQL temporário terminou antes de ficar pronto.' }
        $previousProbePreference = $ErrorActionPreference
        try {
            # Windows PowerShell 5.1 trata stderr nativo como erro terminante
            # quando Stop está ativo; ligação recusada durante o arranque é esperada.
            $ErrorActionPreference = 'Continue'
            $reportedDataPath = & $clientPath --protocol=TCP --host=127.0.0.1 "--port=$testPort" --user=root --connect-timeout=1 --batch --skip-column-names '--execute=SELECT @@datadir;' 2>$null
            $probeExitCode = $LASTEXITCODE
        }
        finally { $ErrorActionPreference = $previousProbePreference }
        if ($probeExitCode -eq 0) {
            $actualDataPath = [IO.Path]::GetFullPath([string]$reportedDataPath).TrimEnd('\', '/')
            if ($actualDataPath -ne [IO.Path]::GetFullPath($dataPath).TrimEnd('\', '/')) {
                throw 'A porta não corresponde à instância temporária esperada.'
            }
            $serverReady = $true
            break
        }
        Start-Sleep -Milliseconds 500
    }
    if (-not $serverReady) { throw 'O MySQL temporário não ficou pronto.' }
    Remove-Item -LiteralPath $initPath -Force
    [Environment]::SetEnvironmentVariable('MYSQL_PWD', $previousMySqlPassword, 'Process')
    $env:SCHOOLGETHER_TEST_MYSQL = "Server=127.0.0.1;Port=$testPort;Database=schoolgether_test_identity;User=schoolgether_test;Password=$testPassword;SslMode=Required;"
    Write-Host 'A testar migrações, contas, permissões, isolamento entre instituições e sessões...'
    $artifactArguments = @()
    if ($ArtifactsPath) { $artifactArguments = @('--artifacts-path', $ArtifactsPath) }
    $testFilter = if ($Scenario -eq 'Onboarding') { 'FullyQualifiedName~MySqlOnboardingTests' } elseif ($Scenario -eq 'InstitutionCapacity') { 'FullyQualifiedName~InstitutionCapacityTests.One_remaining_seat_allows_only_one_concurrent_creation' } elseif ($Scenario -eq 'InstitutionSetup') { 'FullyQualifiedName~MySqlIdentityTests.Setup_upgrade_preserves_legacy_data_and_serializes_concurrent_edits' } else { 'FullyQualifiedName~MySqlIdentityTests.Identity_WithMySqlMigrations_IsolatesInstitutionsAndRevokesSessions' }
    & dotnet test $testProject @artifactArguments --configuration Release --filter $testFilter --logger 'trx;LogFileName=mysql-identity.trx' --results-directory $runPath
    $testExitCode = $LASTEXITCODE
    if ($testExitCode -eq 0) {
        [xml]$testReport = Get-Content -LiteralPath (Join-Path $runPath 'mysql-identity.trx') -Raw
        $counters = $testReport.TestRun.ResultSummary.Counters
        if ([int]$counters.executed -ne $ExpectedTestCount -or [int]$counters.passed -ne $ExpectedTestCount) {
            throw 'O cenário MySQL não foi executado e aprovado; testes ignorados não contam como validação.'
        }
    }
}
finally {
    try {
        if ($null -ne $listener) { $listener.Stop() }
        if ($null -ne $serverProcess) {
            try {
                if ($serverReady) {
                    $env:MYSQL_PWD = $testPassword
                    & $clientPath --protocol=TCP --host=127.0.0.1 "--port=$testPort" --user=root --connect-timeout=2 '--execute=SHUTDOWN;' 2>$null
                    $null = $serverProcess.WaitForExit(5000)
                }
            }
            finally {
                # Em Windows o mysqld pode criar um processo filho. Parar apenas
                # processos cuja linha de comando aponta para este datadir aleatório.
                $ownedProcesses = @(Get-CimInstance Win32_Process -Filter "Name = 'mysqld.exe'" |
                    Where-Object { $_.CommandLine -and $_.CommandLine.Contains($dataPath) })
                foreach ($ownedProcess in $ownedProcesses) {
                    $processToStop = Get-Process -Id $ownedProcess.ProcessId -ErrorAction SilentlyContinue
                    if ($null -ne $processToStop) {
                        try {
                            Stop-Process -Id $processToStop.Id -ErrorAction Stop
                            $null = $processToStop.WaitForExit(5000)
                        }
                        catch { if (-not $processToStop.HasExited) { throw } }
                        finally { $processToStop.Dispose() }
                    }
                }
            }
        }
    }
    finally {
        # Restaurar sempre o ambiente, mesmo que o encerramento de um processo falhe.
        [Environment]::SetEnvironmentVariable('MYSQL_PWD', $previousMySqlPassword, 'Process')
        [Environment]::SetEnvironmentVariable('SCHOOLGETHER_TEST_MYSQL', $previousTestConnection, 'Process')
        try {
            # Só remover o diretório aleatório criado por esta execução, dentro de TEMP.
            $resolvedRunPath = [IO.Path]::GetFullPath($runPath)
            $expectedRunPath = [IO.Path]::GetFullPath((Join-Path $temporaryRoot ('schoolgether-mysql-test-' + $runId)))
            if ($resolvedRunPath -ne $expectedRunPath -or
                -not $resolvedRunPath.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'O caminho temporário não passou a verificação de segurança para limpeza.'
            }
            for ($cleanupAttempt = 0; $cleanupAttempt -lt 10; $cleanupAttempt++) {
                if (-not (Test-Path -LiteralPath $resolvedRunPath)) { break }
                try {
                    Remove-Item -LiteralPath $resolvedRunPath -Recurse -Force -ErrorAction Stop
                    break
                }
                catch {
                    if ($cleanupAttempt -eq 9) { throw }
                    Start-Sleep -Milliseconds 500
                }
            }
        }
        finally {
            if ($null -ne $serverProcess) { $serverProcess.Dispose() }
            $testPassword = $null
            $initSql = $null
        }
    }
}
if ($testExitCode -ne 0) { throw 'Os testes MySQL falharam; consulte o resultado acima.' }
Write-Host 'Testes MySQL aprovados. Instância e dados temporários removidos.'
