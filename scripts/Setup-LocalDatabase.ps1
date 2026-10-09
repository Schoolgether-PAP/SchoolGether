#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$MySqlPath = 'C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe'
)

$ErrorActionPreference = 'Stop'
$repositoryPath = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repositoryPath 'src\Backend\SchoolGether.Api\SchoolGether.Api.csproj'

if (-not (Test-Path -LiteralPath $MySqlPath -PathType Leaf)) {
    throw 'mysql.exe não encontrado. Indique o caminho com -MySqlPath.'
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Instale o SDK .NET 10 antes de executar este assistente.'
}

Write-Host 'Este assistente cria schoolgether e o utilizador schoolgether_dev@localhost.'
Write-Host 'Se o utilizador já existir, o assistente termina sem alterar a sua palavra-passe.'
$rootPassword = Read-Host 'Palavra-passe do root do MySQL local' -AsSecureString
$passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($rootPassword)
$previousMySqlPassword = [Environment]::GetEnvironmentVariable('MYSQL_PWD', 'Process')
$randomBytes = New-Object byte[] 32
$randomGenerator = [Security.Cryptography.RandomNumberGenerator]::Create()
$randomGenerator.GetBytes($randomBytes)
$randomGenerator.Dispose()
$applicationPassword = 'Sg!' + [Convert]::ToBase64String($randomBytes) + '7'

try {
    # A palavra-passe não é passada nos argumentos nem escrita no repositório.
    $env:MYSQL_PWD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    $existingUser = & $MySqlPath --host=localhost --user=root --batch --skip-column-names --execute="SELECT COUNT(*) FROM mysql.user WHERE User = 'schoolgether_dev' AND Host = 'localhost';"
    if ($LASTEXITCODE -ne 0) {
        throw 'Não foi possível entrar no MySQL local. Confirme o serviço e a palavra-passe do root.'
    }
    if ([int]$existingUser -ne 0) {
        throw 'schoolgether_dev@localhost já existe. Consulte docs/database.md para reutilizar a configuração.'
    }

    # Guardar primeiro a ligação permite recuperá-la se a criação MySQL falhar.
    # O processo dotnet não recebe a palavra-passe de root no ambiente.
    [Environment]::SetEnvironmentVariable('MYSQL_PWD', $previousMySqlPassword, 'Process')
    $connectionString = "Server=localhost;Port=3306;Database=schoolgether;User=schoolgether_dev;Password=$applicationPassword;"
    @{ 'ConnectionStrings:SchoolGether' = $connectionString } |
        ConvertTo-Json -Compress |
        & dotnet user-secrets set --project $apiProject
    if ($LASTEXITCODE -ne 0) {
        throw 'Não foi possível guardar a ligação em user-secrets. A base e o utilizador ainda não foram criados.'
    }

    $env:MYSQL_PWD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)

    # Só a base e as permissões são criadas aqui. As tabelas pertencem às migrações EF.
    $bootstrapSql = @"
CREATE DATABASE IF NOT EXISTS schoolgether CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER 'schoolgether_dev'@'localhost' IDENTIFIED BY '$applicationPassword';
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, DROP, INDEX, REFERENCES
ON schoolgether.* TO 'schoolgether_dev'@'localhost';
"@
    $bootstrapSql | & $MySqlPath --host=localhost --user=root --default-character-set=utf8mb4
    if ($LASTEXITCODE -ne 0) {
        throw 'A configuração MySQL falhou. Verifique no Workbench se a base ou o utilizador foram criados antes de repetir.'
    }

}
finally {
    [Environment]::SetEnvironmentVariable('MYSQL_PWD', $previousMySqlPassword, 'Process')
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    $rootPassword.Dispose()
    $applicationPassword = $null
    $connectionString = $null
    $bootstrapSql = $null
}

Write-Host 'Base e utilizador criados. Ligação guardada em user-secrets da API.'
Write-Host 'Próximo passo: aplicar a migração seguindo docs/database.md.'
