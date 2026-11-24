param(
 [Parameter(Mandatory)][string]$OutputDirectory,
 [string]$BasePath='/SchoolGether/',
 [string]$ApiBaseUrl='https://schoolgether-api.runasp.net/'
)
$ErrorActionPreference='Stop'
if($BasePath-notmatch '^/(?:[a-zA-Z0-9_-]+/)*$'){throw 'BasePath inválido.'}
$repo=Split-Path $PSScriptRoot
$web=Join-Path $repo 'src/Clients/SchoolGether.Web'
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path $output){throw 'Escolher uma pasta de saída nova para preservar versões anteriores.'}
$build=Join-Path $output 'blazor-build'
& dotnet publish (Join-Path $web 'SchoolGether.Web.csproj') -c Release -o $build
if($LASTEXITCODE-ne 0){throw 'Publicação Blazor falhou.'}
$bundle=Join-Path $output 'site'
[void](New-Item -ItemType Directory -Path $bundle)
Copy-Item (Join-Path $web 'Marketing/*') $bundle -Recurse
Copy-Item (Join-Path $build 'wwwroot') (Join-Path $bundle 'app') -Recurse
$index=Join-Path $bundle 'app/index.html'
$content=[IO.File]::ReadAllText($index).Replace('<base href="/" />',"<base href=`"${BasePath}app/`" />")
[IO.File]::WriteAllText($index,$content,[Text.UTF8Encoding]::new($false))
@{Api=@{BaseUrl=$ApiBaseUrl}}|ConvertTo-Json -Depth 3|Set-Content (Join-Path $bundle 'app/appsettings.json') -Encoding utf8
# As versões comprimidas destes ficheiros foram geradas antes da configuração do pacote.
foreach($name in @('index.html.br','index.html.gz','appsettings.json.br','appsettings.json.gz')){
 $file=Join-Path $bundle "app/$name"
 if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file}
}
# Pages serve ficheiros estáticos, incluindo a aplicação em app/.
''|Set-Content (Join-Path $bundle '.nojekyll')
$commit=& git -c "safe.directory=$($repo.Replace('\','/'))" -C $repo rev-parse HEAD
@{sourceCommit=$commit;builtAt=[DateTime]::UtcNow.ToString('o');basePath=$BasePath;apiBaseUrl=$ApiBaseUrl}|ConvertTo-Json|Set-Content (Join-Path $bundle 'deployment-manifest.json')
$privateFiles=Get-ChildItem $bundle -Recurse -File|Where-Object{$_.Name-match 'publishsettings|dpapi|\.pem$|\.pfx$|web\.config|appsettings\.Production\.json'}
if($privateFiles){throw 'O pacote contém ficheiros que não devem ser publicados.'}
"Pacote preparado: $bundle"
