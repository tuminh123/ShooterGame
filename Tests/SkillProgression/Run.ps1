param([string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location $projectRoot
try {
    $output = Join-Path $projectRoot 'Temp/SkillProgressionVerification'
    New-Item -ItemType Directory -Force $output | Out-Null
    $dotnetRoot = Split-Path (Get-Command dotnet).Source -Parent
    $referencePack = Get-ChildItem (Join-Path $dotnetRoot 'packs/Microsoft.NETCore.App.Ref') -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    $referenceFramework = Get-ChildItem (Join-Path $referencePack.FullName 'ref') -Directory | Select-Object -First 1
    $math = Join-Path $projectRoot 'Library/ScriptAssemblies/Unity.Mathematics.dll'
    if (-not (Test-Path -LiteralPath $math)) { throw 'Import the project in Unity first to create Unity.Mathematics.dll.' }
    Copy-Item -LiteralPath $math -Destination $output -Force
    $argsFile = @('-nologo','-target:exe','-nowarn:0649',('-out:"' + (Join-Path $output 'Checks.dll') + '"'))
    $argsFile += Get-ChildItem $referenceFramework.FullName -Filter *.dll | ForEach-Object { '-r:"' + $_.FullName + '"' }
    $argsFile += '-r:"' + $math + '"'
    $sources = @(
        'UI/SkillChoice/SkillChoicePool.cs','UI/Core/UIManager.cs','Core/GameFlow/GameState.cs','Core/GameFlow/GameUIEventBus.cs','Core/GameFlow/GameManager.cs','UI/Gameplay/GameplayController.cs','UI/Core/UIPanelController.cs','UI/SkillChoice/SkillChoiceController.cs','Core/Run/GameplayPauseService.cs','Characters/Stats/BaseStats.cs','Characters/Stats/StatBonus.cs','Characters/Stats/StatModifierLease.cs','Characters/Stats/StatModifier.cs','Characters/Stats/StatCalculator.cs',
        'Characters/Controllers/ICharacterController.cs','Characters/Controllers/ICharacterFeedbackSource.cs','Characters/Controllers/CharacterControllerBase.cs','Characters/Enemies/WaveConfigSO.cs','Characters/Enemies/WaveProgress.cs','Levels/Configs/CharacterLevelConfigSO.cs','Characters/Stats/CharacterRuntimeData.cs','Characters/Data/CharacterType.cs','Characters/Data/CharacterTeam.cs',
        'Characters/CharacterSearch/CharacterTarget.cs','Core/JobSystem/Spatial/SpatialGeometry.cs',
        'Combat/Damage/DamageInfo.cs','Characters/Enemies/EnemyCombatFeedback.cs','Characters/Enemies/SpriteDeathAnimation.cs','Combat/Effects/Gameplay/GameplayEffects.cs','Combat/Effects/Gameplay/EffectRuntime.cs','Combat/Effects/Gameplay/EffectManager.cs','Combat/Effects/Gameplay/EffectRuntimePool.cs',
        'Combat/Skills/ISkill.cs','Combat/Skills/SkillDataSO.cs','Combat/Skills/SkillDatabaseSO.cs','Combat/Skills/SkillRuntime.cs',
        'Combat/Skills/SkillManager.cs','Combat/Skills/CharacterSkills.cs','Combat/Skills/SkillFactory.cs',
        'Combat/Skills/Behaviours/StatBonusSkill.cs','Combat/Skills/Behaviours/NormalAttackSkill.cs','Combat/Skills/Behaviours/RegenerationSkill.cs','Combat/Skills/Behaviours/EmergencyShieldSkill.cs','Combat/Skills/Behaviours/ShootingSkill.cs',
        'Combat/Skills/Behaviours/OrbitSawSkill.cs','Combat/Skills/Behaviours/FrostAuraSkill.cs',
        'Combat/Skills/Data/ShootingData.cs','Combat/Skills/Data/NormalAttackData.cs','Combat/Skills/Data/OrbitSawData.cs','Combat/Skills/Data/FrostAuraData.cs','Combat/Skills/Data/RegenerationData.cs','Combat/Skills/Data/EmergencyShieldData.cs','Combat/Skills/Data/StatBonusData.cs','Combat/Skills/Skill.cs','Combat/Skills/Data/SkillLevelData.cs','Combat/Skills/Data/SkillBehaviourData.cs',
        'Combat/ShootingSkill/Shooters.cs','Combat/ShootingSkill/Projectiles/ProjectileMoves.cs'
    )
    $argsFile += $sources | ForEach-Object { '"Assets/_Project/Scripts/' + $_ + '"' }
    $argsFile += '"Tests/SkillProgression/TestEnvironment.cs"','"Tests/SkillProgression/TweenTestEnvironment.cs"','"Tests/SkillProgression/Program.cs"'
    $response = Join-Path $output 'checks.rsp'
    $argsFile | Set-Content -LiteralPath $response -Encoding UTF8
    & (Join-Path $UnityEditor 'Data/NetCoreRuntime/dotnet.exe') (Join-Path $UnityEditor 'Data/DotNetSdkRoslyn/csc.dll') ('@' + $response)
    if ($LASTEXITCODE -ne 0) { throw 'Check compilation failed.' }
    @{ runtimeOptions = @{ tfm = $referenceFramework.Name; framework = @{ name = 'Microsoft.NETCore.App'; version = $referencePack.Name }; rollForward = 'LatestMajor' } } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $output 'Checks.runtimeconfig.json')
    dotnet (Join-Path $output 'Checks.dll')
    if ($LASTEXITCODE -ne 0) { throw 'Skill checks failed.' }
}
finally { Pop-Location }
