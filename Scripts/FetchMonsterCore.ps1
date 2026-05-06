#Requires -Version 5.1
<#
.SYNOPSIS
    Fetches Pathfinder 2e Monster Core creature data from the Foundry VTT PF2e GitHub repo
    and converts it to the DndBuilder schema.

.OUTPUTS
    Data/Pf2e/Monster_Core.json      — mechanical data (tracked in git)
    Data/Pf2e/Monster_Core_Lore.json — lore/description (gitignored)

.NOTES
    Run from the repo root: powershell -File Scripts\FetchMonsterCore.ps1
    GitHub API: ~60 requests/hour unauthenticated. Set $Token to your PAT for 5000/hour.
    Example with token: powershell -File Scripts\FetchMonsterCore.ps1 -Token ghp_xxx
#>
param(
    [string]$Token     = "",
    [string]$Pack      = "pathfinder-monster-core",
    [string]$OutDir    = "$PSScriptRoot\..\Data\Pf2e",
    [int]   $BatchSize = 50,
    [switch]$SkipLore
)

Set-StrictMode -Off
$ErrorActionPreference = 'Continue'

function Invoke-GH([string]$Url) {
    $h = @{ 'Accept'='application/vnd.github.v3+json'; 'User-Agent'='DndBuilder-Converter' }
    if ($Token) { $h['Authorization'] = "Bearer $Token" }
    try { Invoke-RestMethod -Uri $Url -Headers $h -TimeoutSec 30 } catch { Write-Warning "GH: $_"; $null }
}
function Invoke-Raw([string]$Url) {
    $h = @{ 'User-Agent'='DndBuilder-Converter' }
    if ($Token) { $h['Authorization'] = "Bearer $Token" }
    try { Invoke-RestMethod -Uri $Url -Headers $h -TimeoutSec 30 } catch { Write-Warning "Raw: $_"; $null }
}
function Def($v, $d)  { if ($null -ne $v -and "$v" -ne '') { $v } else { $d } }
function INT($v)       { [int](Def $v 0) }
function TC([string]$s) { if ($s -and $s.Length -gt 0) { $s.Substring(0,1).ToUpper() + $s.Substring(1) } else { $s } }

function Resolve-Size([string]$s) {
    switch ($s) { 'tiny'{'Tiny'} 'sm'{'Small'} 'med'{'Medium'} 'lg'{'Large'} 'huge'{'Huge'} 'grg'{'Gargantuan'} default{'Medium'} }
}
function Resolve-ActionCost($at, $av) {
    if ($at -eq 'passive')  { return 'None' }
    if ($at -eq 'reaction') { return 'Reaction' }
    if ($at -eq 'free')     { return 'Free Action' }
    if ($at -eq 'action') {
        $n = if ($null -ne $av) { [int]$av } else { 1 }
        switch ($n) { 1{'1 Action'} 2{'2 Actions'} 3{'3 Actions'} default{'1 Action'} }
    }
    return 'None'
}
function Resolve-AbilityType([string]$cat) {
    switch ($cat) { 'offensive'{'Offensive'} 'defensive'{'Defensive'} 'interaction'{'Interaction'} default{'General'} }
}
function Resolve-CreatureType([string[]]$traits) {
    $tt = @('aberration','animal','astral','beast','celestial','construct','dragon','dream','elemental','fey','fiend','fungus','ghost','humanoid','monitor','ooze','plant','spirit','swarm','time','undead')
    foreach ($t in $tt) { if ($traits -contains $t) { return $t.Substring(0,1).ToUpper() + $t.Substring(1) } }
    return 'Unknown'
}
function Resolve-Rarity([string]$r) {
    switch ($r) { 'uncommon'{'Uncommon'} 'rare'{'Rare'} 'unique'{'Unique'} default{'Common'} }
}
function Strip-Html([string]$h) {
    if (-not $h) { return '' }
    ($h -replace '<[^>]+>',' ' -replace '&amp;','&' -replace '&lt;','<' -replace '&gt;','>' -replace '&nbsp;',' ' -replace '&quot;','"' -replace '\s+',' ').Trim()
}

function Convert-Creature($raw) {
    $sys = $raw.system
    $traits = @($sys.traits.value | Where-Object { $_ } | ForEach-Object { "$_" })
    $traitsTitled = $traits | ForEach-Object { TC $_ }

    $otherSpeeds = @()
    if ($sys.attributes.speed.otherSpeeds) {
        foreach ($sp in $sys.attributes.speed.otherSpeeds) {
            $otherSpeeds += [PSCustomObject]@{ type=$sp.type; value=INT($sp.value) }
        }
    }
    $senses = @()
    if ($sys.perception.senses) {
        foreach ($sn in $sys.perception.senses) {
            $rng = if ($sn.range -and "$($sn.range)" -ne '' -and [int]$sn.range -gt 0) { [int]$sn.range } else { $null }
            $senses += [PSCustomObject]@{ type=$sn.type; range=$rng }
        }
    }
    $imm  = @()
    if ($sys.attributes.immunities)  { $imm  = @($sys.attributes.immunities  | ForEach-Object { $_.type }) }
    $res  = @()
    if ($sys.attributes.resistances) {
        foreach ($r in $sys.attributes.resistances) { $res  += [PSCustomObject]@{ type=$r.type; value=INT($r.value) } }
    }
    $weak = @()
    if ($sys.attributes.weaknesses) {
        foreach ($w in $sys.attributes.weaknesses) { $weak += [PSCustomObject]@{ type=$w.type; value=INT($w.value) } }
    }
    $langs = @()
    if ($sys.details.languages.value) { $langs = @($sys.details.languages.value) }

    $skills = [ordered]@{}
    if ($sys.skills) {
        foreach ($prop in $sys.skills.PSObject.Properties) {
            $skills[$prop.Name] = INT($prop.Value.base)
        }
    }

    $items = @($raw.items | Where-Object { $_ })
    $spellEntries = @{}
    foreach ($item in $items) {
        if ($item.type -eq 'spellcastingEntry') {
            $spellEntries[$item._id] = [PSCustomObject]@{
                dc=INT($item.system.spelldc.dc)
                attack=INT($item.system.spelldc.value)
                tradition=(Def $item.system.tradition.value '')
            }
        }
    }

    $strikes = @()
    foreach ($item in $items) {
        if ($item.type -ne 'melee') { continue }
        $isMelee = ($null -eq $item.system.range -or "$($item.system.range)" -eq '')
        $ab = INT($item.system.bonus.value)
        $isAgile = @($item.system.traits.value) -contains 'agile'
        $ab2 = if ($ab -ne 0) { if ($isAgile) { $ab-4 } else { $ab-5 } } else { $null }
        $ab3 = if ($ab -ne 0) { if ($isAgile) { $ab-8 } else { $ab-10 } } else { $null }
        $dmgRolls = @()
        if ($item.system.damageRolls) {
            foreach ($k in $item.system.damageRolls.PSObject.Properties) {
                $dmgRolls += [PSCustomObject]@{ damage=$k.Value.damage; type=$k.Value.damageType }
            }
        }
        $sTraits = @($item.system.traits.value | Where-Object { $_ } | ForEach-Object { TC "$_" })
        $rangeFt = $null
        if (-not $isMelee -and $item.system.range) { $rangeFt = INT($item.system.range.increment) }
        $strikes += [PSCustomObject]@{
            name=$item.name; is_melee=$isMelee; attack_bonus=$ab; attack_bonus_2=$ab2; attack_bonus_3=$ab3
            damage_rolls=$dmgRolls; range_feet=$rangeFt; traits=$sTraits
        }
    }

    $abilities = @()
    foreach ($item in $items) {
        if ($item.type -notin @('action','feat')) { continue }
        $cat = if ($item.system.category) { "$($item.system.category)" } else { 'offensive' }
        $at  = if ($item.system.actionType.value) { "$($item.system.actionType.value)" } else { 'passive' }
        $av  = $item.system.actions.value
        $trigger = if ($item.system.trigger) { Strip-Html("$($item.system.trigger.value)") } else { '' }
        $effect  = Strip-Html("$($item.system.description.value)")
        $aTraits = @($item.system.traits.value | Where-Object { $_ } | ForEach-Object { TC "$_" })
        $abil = [ordered]@{
            name=$item.name; ability_type=(Resolve-AbilityType $cat); action_cost=(Resolve-ActionCost $at $av)
            trigger=$trigger; effect_text=$effect; traits=$aTraits
        }
        if ($item.system.location.value -and $spellEntries.ContainsKey($item.system.location.value)) {
            $e = $spellEntries[$item.system.location.value]
            $abil['spell_dc']=$e.dc; $abil['spell_attack']=$e.attack; $abil['tradition']=$e.tradition
        }
        $abilities += [PSCustomObject]$abil
    }
    foreach ($item in $items) {
        if ($item.type -ne 'spellcastingEntry') { continue }
        $trad = Def $item.system.tradition.value ''
        $prep = Def $item.system.prepared.value ''
        $abilities += [PSCustomObject]@{
            name=$item.name; ability_type='Spellcasting'; action_cost='None'; trigger=''
            effect_text="Tradition: $trad | $prep"; traits=@()
            spell_dc=INT($item.system.spelldc.dc); spell_attack=INT($item.system.spelldc.value); tradition=$trad
        }
    }

    $pub    = $sys.details.publication
    $source = if ($pub -and $pub.title) { "$($pub.title)" } else { 'Pathfinder Monster Core' }
    $srcPg  = if ($pub -and $pub.page -and "$($pub.page)" -ne '') { [int]$pub.page } else { $null }

    $mech = [PSCustomObject]@{
        name=$raw.name
        level=INT($sys.details.level.value)
        ac=INT($sys.attributes.ac.value); max_hp=INT($sys.attributes.hp.max); hp_special=(Def $sys.attributes.hp.details '')
        str_mod=INT($sys.abilities.str.mod); dex_mod=INT($sys.abilities.dex.mod); con_mod=INT($sys.abilities.con.mod)
        int_mod=INT($sys.abilities.int.mod); wis_mod=INT($sys.abilities.wis.mod); cha_mod=INT($sys.abilities.cha.mod)
        fortitude=INT($sys.saves.fortitude.value); reflex=INT($sys.saves.reflex.value); will=INT($sys.saves.will.value)
        perception=INT($sys.perception.mod)
        size=(Resolve-Size $sys.traits.size.value); creature_type=(Resolve-CreatureType $traits); rarity=(Resolve-Rarity $sys.traits.rarity)
        traits=$traitsTitled; source=$source; source_page=$srcPg
        speed_walk=INT($sys.attributes.speed.value); other_speeds=$otherSpeeds; senses=$senses
        immunities=$imm; resistances=$res; weaknesses=$weak; languages=$langs; skills=$skills
        strikes=$strikes; abilities=$abilities
    }
    $lore = [PSCustomObject]@{ name=$raw.name; description=(Def $sys.details.publicNotes '') }
    return @{ mech=$mech; lore=$lore }
}

# ── main ─────────────────────────────────────────────────────────────────────

if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }

$apiBase = "https://api.github.com/repos/foundryvtt/pf2e/contents/packs/pf2e/$Pack"
Write-Host "Fetching file list from: $apiBase" -ForegroundColor Cyan
$fileList = Invoke-GH $apiBase
if (-not $fileList) { Write-Error "Could not fetch file list. Check rate limits or provide -Token."; exit 1 }

$jsonFiles = @($fileList | Where-Object { $_.name -like '*.json' })
Write-Host "Found $($jsonFiles.Count) creature files." -ForegroundColor Green

$allMech = @()
$allLore = @()
$done    = 0
$failed  = @()

foreach ($file in $jsonFiles) {
    $rawUrl  = if ($file.download_url) { $file.download_url } else { "https://raw.githubusercontent.com/foundryvtt/pf2e/v14-dev/packs/pf2e/$Pack/$($file.name)" }
    $rawData = Invoke-Raw $rawUrl
    if (-not $rawData) { $failed += $file.name; $done++; continue }

    try {
        $c = Convert-Creature $rawData
        $allMech += $c.mech
        $allLore += $c.lore
    } catch {
        Write-Warning "Conversion failed for $($file.name): $_"
        $failed += $file.name
    }

    $done++
    if ($done % 10 -eq 0) { Write-Host "  $done / $($jsonFiles.Count) ..." -ForegroundColor Gray }
    if ($done % $BatchSize -eq 0) { Write-Host "  Batch done, sleeping 3s..." -ForegroundColor DarkGray; Start-Sleep -Seconds 3 }
}

$mechPath = Join-Path $OutDir "Monster_Core.json"
$lorePath = Join-Path $OutDir "Monster_Core_Lore.json"

$allMech | ConvertTo-Json -Depth 15 | Out-File -FilePath $mechPath -Encoding utf8
Write-Host "Saved: $mechPath ($($allMech.Count) creatures)" -ForegroundColor Green

if (-not $SkipLore) {
    $allLore | ConvertTo-Json -Depth 5 | Out-File -FilePath $lorePath -Encoding utf8
    Write-Host "Saved: $lorePath" -ForegroundColor Green
}

if ($failed.Count -gt 0) { Write-Warning "$($failed.Count) failed: $($failed -join ', ')" }
Write-Host "Done! $($allMech.Count) creatures converted." -ForegroundColor Cyan
