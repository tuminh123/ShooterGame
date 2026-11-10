param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location $projectRoot
try {
    $output = Join-Path $projectRoot 'Temp/EffectPlaybackVerification'
    New-Item -ItemType Directory -Force $output | Out-Null
    $dotnetRoot = Split-Path (Get-Command dotnet).Source -Parent
    $referencePack = Get-ChildItem (Join-Path $dotnetRoot 'packs/Microsoft.NETCore.App.Ref') -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    $referenceFramework = Get-ChildItem (Join-Path $referencePack.FullName 'ref') -Directory | Select-Object -First 1
    $compilerArgs = @('-nologo', '-target:exe', '-nowarn:0649', ('-out:"' + (Join-Path $output 'Checks.dll') + '"'))
    $compilerArgs += Get-ChildItem $referenceFramework.FullName -Filter *.dll | ForEach-Object { '-r:"' + $_.FullName + '"' }
    $compilerArgs += @('"Assets/_Project/Scripts/Combat/Effects/Visuals/IEffectVisual.cs"', '"Assets/_Project/Scripts/Combat/Effects/Visuals/EffectView.cs"', '"Assets/_Project/Scripts/Combat/Effects/Visuals/AnimationEffectVisual.cs"', '"Assets/_Project/Scripts/Combat/Effects/Visuals/ParticleEffectVisual.cs"', '"Tests/EffectPlayback/Program.cs"')
    $response = Join-Path $output 'checks.rsp'
    $compilerArgs | Set-Content -LiteralPath $response -Encoding UTF8
    & (Join-Path $UnityEditor 'Data/NetCoreRuntime/dotnet.exe') (Join-Path $UnityEditor 'Data/DotNetSdkRoslyn/csc.dll') ('@' + $response)
    if ($LASTEXITCODE -ne 0) { throw 'Effect check compilation failed.' }
    @{ runtimeOptions = @{ tfm = $referenceFramework.Name; framework = @{ name = 'Microsoft.NETCore.App'; version = $referencePack.Name }; rollForward = 'LatestMajor' } } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $output 'Checks.runtimeconfig.json')
    dotnet (Join-Path $output 'Checks.dll')
    if ($LASTEXITCODE -ne 0) { throw 'Effect playback checks failed.' }
}
finally { Pop-Location }
