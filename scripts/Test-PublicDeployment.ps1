param(
 [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedSourceCommit,
 [uri]$SiteUrl='https://schoolgether-pap.github.io/SchoolGether/',
 [uri]$ApiUrl='https://schoolgether-api.runasp.net/'
)
$ErrorActionPreference='Stop'
if($SiteUrl.Scheme-ne 'https'-or $ApiUrl.Scheme-ne 'https'){throw 'Usar os endereços HTTPS públicos.'}
$headers=@{'Cache-Control'='no-cache'}
$manifest=Invoke-RestMethod ([uri]::new($SiteUrl,'deployment-manifest.json')) -Headers $headers -TimeoutSec 30
if($manifest.sourceCommit-ne $ExpectedSourceCommit){
 throw "A versão publicada é $($manifest.sourceCommit), mas era esperada $ExpectedSourceCommit. Um merge não publica automaticamente os artefactos."
}
$html=(Invoke-WebRequest $SiteUrl -Headers $headers -TimeoutSec 30).Content
if($html.Contains('Preços de demonstração')){throw 'A página pública ainda contém a mensagem antiga dos planos.'}
$css=(Invoke-WebRequest ([uri]::new($SiteUrl,'refinements.css')) -Headers $headers -TimeoutSec 30).Content
if($css-notmatch '\.template-3\s+\.plan-row\s*\{[^}]*border:\s*1px\s+solid\s+#000\s*;'){
 throw 'Os cartões públicos ainda não têm a borda preta uniforme.'
}
if($css-match '\.plan-row:nth-child\(2\)\s*\{\s*border-color:'){
 throw 'Existe uma regra que altera apenas a borda do segundo plano.'
}
$appHtml=(Invoke-WebRequest ([uri]::new($SiteUrl,'app/')) -Headers $headers -TimeoutSec 30).Content
if(!$appHtml.Contains('<base href="/SchoolGether/app/" />')){throw 'A aplicação publicada tem uma base inesperada.'}
$appCss=(Invoke-WebRequest ([uri]::new($SiteUrl,'app/SchoolGether.Web.styles.css')) -Headers $headers -TimeoutSec 30).Content
if(!$appCss.Contains('wizard-layout')-or !$appCss.Contains('repeat(5,minmax(0,1fr))')){
 throw 'Os recursos publicados ainda não contêm a adesão de cinco passos.'
}
$health=(Invoke-WebRequest ([uri]::new($ApiUrl,'api/v1/health')) -TimeoutSec 35).StatusCode
$ready=(Invoke-WebRequest ([uri]::new($ApiUrl,'api/v1/health/ready')) -TimeoutSec 25).StatusCode
if($health-ne 200-or $ready-ne 200){throw 'A API publicada não está pronta.'}
[pscustomobject]@{SourceCommit=$manifest.sourceCommit;PlanBorders='Black';OldPriceNotice=$false;SignupResources='FiveSteps';Health=$health;Ready=$ready;CheckedAt=[DateTime]::UtcNow.ToString('o')}
