$packageName = 'markdownmonster'
$fileType = 'exe'
$url = 'https://github.com/RickStrahl/MarkdownMonsterReleases/raw/master/v1.7/MarkdownMonsterSetup-1.7.2.exe'

$silentArgs = '/VERYSILENT'
$validExitCodes = @(0)

Install-ChocolateyPackage "packageName" "$fileType" "$silentArgs" "$url"  -validExitCodes  $validExitCodes  -checksum "2B427F53804FB6E5F51878EC190A56ED1FCBA6D324D7334684E213F939A50180" -checksumType "sha256"
