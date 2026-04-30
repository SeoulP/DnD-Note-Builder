param([int]$Count = 5)
$ErrorActionPreference = 'Continue'

function Invoke-GH([string]$Url) {
    $h = @{ 'Accept'='application/vnd.github.v3+json'; 'User-Agent'='DndBuilder' }
    try { Invoke-RestMethod -Uri $Url -Headers $h -TimeoutSec 30 } catch { $null }
}
function Invoke-Raw([string]$Url) {
    $h = @{ 'User-Agent'='DndBuilder' }
    try { Invoke-RestMethod -Uri $Url -Headers $h -TimeoutSec 30 } catch { $null }
}
function Def($v, $d) { if ($null -ne $v -and "$v" -ne '') { $v } else { $d } }
function INT($v) { [int](Def $v 0) }

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
function TC([string]$s) { if ($s -and $s.Length -gt 0) { $s.Substring(0,1).ToUpper() + $s.Substring(1) } else { $s } }

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
        foreach ($sn in $sys.perception.senses) { $senses += [PSCustomObject]@{ type=$sn.type } }
    }
    $imm  = @()
    if ($sys.attributes.immunities)  { $imm  = @($sys.attributes.immunities  | ForEach-Object { $_.type }) }
    $res  = @()
    if ($sys.attributes.resistances) {
        foreach ($r in $sys.attributes.resistances) { $res  += [PSCustomObject]@{ type=$r.type; value=INT($r.value) } }
    }
    $weak = @()
    if ($sys.attributes.weaknesses) {
        foreach ($w in $sys.attributes.weaknesses)  { $weak += [PSCustomObject]@{ type=$w.type; value=INT($w.value) } }
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
        $isMelee = ($null -eq $item.system.range -or $item.system.range -eq '')
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
        $cat = if ($item.system.category) { $item.system.category } else { 'offensive' }
        $at  = if ($item.system.actionType.value) { $item.system.actionType.value } else { 'passive' }
        $av  = $item.system.actions.value
        $trigger = if ($item.system.trigger) { Strip-Html($item.system.trigger.value) } else { '' }
        $effect  = Strip-Html($item.system.description.value)
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
            effect_text="Tradition: $trad | $prep"
            traits=@(); spell_dc=INT($item.system.spelldc.dc); spell_attack=INT($item.system.spelldc.value)
            tradition=$trad
        }
    }

    $pub    = $sys.details.publication
    $source = if ($pub -and $pub.title) { $pub.title } else { 'Pathfinder Monster Core' }
    $srcPg  = if ($pub -and $pub.page -and $pub.page -ne '') { [int]$pub.page } else { $null }

    $mech = [PSCustomObject]@{
        name=$raw.name
        level=INT($sys.details.level.value)
        ac=INT($sys.attributes.ac.value)
        max_hp=INT($sys.attributes.hp.max)
        hp_special=(Def $sys.attributes.hp.details '')
        str_mod=INT($sys.abilities.str.mod); dex_mod=INT($sys.abilities.dex.mod); con_mod=INT($sys.abilities.con.mod)
        int_mod=INT($sys.abilities.int.mod); wis_mod=INT($sys.abilities.wis.mod); cha_mod=INT($sys.abilities.cha.mod)
        fortitude=INT($sys.saves.fortitude.value); reflex=INT($sys.saves.reflex.value); will=INT($sys.saves.will.value)
        perception=INT($sys.perception.mod)
        size=(Resolve-Size $sys.traits.size.value)
        creature_type=(Resolve-CreatureType $traits)
        rarity=(Resolve-Rarity $sys.traits.rarity)
        traits=$traitsTitled; source=$source; source_page=$srcPg
        speed_walk=INT($sys.attributes.speed.value); other_speeds=$otherSpeeds; senses=$senses
        immunities=$imm; resistances=$res; weaknesses=$weak; languages=$langs; skills=$skills
        strikes=$strikes; abilities=$abilities
    }
    $lore = [PSCustomObject]@{ name=$raw.name; description=(Def $sys.details.publicNotes '') }
    return @{ mech=$mech; lore=$lore }
}

$apiBase = 'https://api.github.com/repos/foundryvtt/pf2e/contents/packs/pf2e/pathfinder-monster-core'
$fileList = Invoke-GH $apiBase
$jsonFiles = @($fileList | Where-Object { $_.name -like '*.json' }) | Select-Object -First $Count

Write-Host "Testing with $($jsonFiles.Count) creatures..."
$allMech = @(); $allLore = @()
foreach ($file in $jsonFiles) {
    $rawData = Invoke-Raw $file.download_url
    if (-not $rawData) { Write-Warning "Failed: $($file.name)"; continue }
    try {
        $c = Convert-Creature $rawData
        $allMech += $c.mech; $allLore += $c.lore
        Write-Host "OK: $($rawData.name) (lv $($rawData.system.details.level.value), $($rawData.items.Count) items)"
    } catch { Write-Warning "ERR $($file.name): $_" }
}

$outDir = 'C:\Users\Seoul\Desktop\Game Development\WIP Games\Godot\dnd-builder\Data\Pf2e'
$allMech | ConvertTo-Json -Depth 15 | Out-File "$outDir\Monster_Core_Test.json" -Encoding utf8
$allLore | ConvertTo-Json -Depth 5  | Out-File "$outDir\Monster_Core_Lore_Test.json" -Encoding utf8
Write-Host "Written $($allMech.Count) creatures to test files."
