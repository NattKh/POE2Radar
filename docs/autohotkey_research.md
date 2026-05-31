# AutoHotKey Automation Research

Research notes for implementing automatic skill/flask key pressing, based on GameHelper's AutoHotKeyTrigger plugin.

## GameHelper AutoHotKeyTrigger Architecture

### Rule System
- Each rule: IF (all conditions true) THEN (press key)
- No combo sequences — each rule is independent, evaluated every frame
- Multiple rules in a Profile, multiple Profiles switchable
- Rules evaluated top-to-bottom, first match wins per frame
- Source: `Plugins/AutoHotKeyTrigger/ProfileManager/Rule.cs`

### Condition Types
From `ProfileManager/Enums/ConditionType.cs`:

| Condition | What it checks | Data source |
|-----------|---------------|-------------|
| VITALS | HP/Mana/ES percent thresholds | Life component |
| ANIMATION | Player animation state (casting, attacking, idle) | Actor component |
| STATUS_EFFECT | Active buffs/debuffs with duration | Buffs component |
| FLASK_IS_ACTIVE | Flask effect currently applied | Flask inventory |
| FLASK_CHARGES | Flask has X charges remaining | Flask inventory |
| FLASK_IS_USEABLE | Flask has enough charges to use | Flask inventory |
| AILMENT | Bleeding, Poison, Freeze, etc. | Status effects |
| IS_SKILL_USEABLE | Skill cooldown ready | Actor.ActiveSkills |
| DEPLOYED_OBJECT_COUNT | Minion/totem count | Actor.DeployedEntities |
| NEARBY_MONSTER_COUNT | Enemies nearby by rarity/distance | Entity list |
| ON_KEY_PRESSED_FOR_ACTION | Player holds a specific key | GetAsyncKeyState |
| WEAPON_SET_ACTIVE | First or second weapon set | Stats component |
| SHAPESHIFTED | Player in animal form | Actor component |
| DYNAMIC | Custom Linq expression | All of the above |

### Dynamic Condition Expressions
Users write Linq-style expressions evaluated at runtime:
```
PlayerVitals.HP.Percent <= 30 && Flasks.Flask1.IsUsable
PlayerBuffs.Has("grace_period") && !PlayerBuffs.Has("haste")
MonsterCount(MonsterRarity.Rare, MonsterNearbyZones.InnerCircle) > 2
PlayerSkillIsUseable.Contains("PhysicalProjectile")
DeployedObjectsCount[0] < 3
```
Source: `DynamicCondition.cs` lines 85-89, 127-139

### Game State Access
`DynamicConditionState.cs` lines 18-122 provides all state:
- **PlayerVitals**: HP, Mana, ES (Current, Max, Percent, Reserved)
- **PlayerBuffs**: Active status effects with duration/charges
- **Flasks**: 5 slots, each with Active/Charges/IsUsable
- **PlayerSkillIsUseable**: Set of skill IDs ready to use
- **PlayerAnimation**: Current animation int value
- **DeployedObjectsCount**: Array of deployed entity counts
- **MonsterCount()**: Enemies by rarity and distance zone

### Keystroke Sending
`GameHelper/Utils/MiscHelper.cs` lines 97-125:
- Uses `SendMessage(hwnd, 0x101, keyCode, 0)` — WM_KEYUP message
- Sent asynchronously via `Task.Run()` to avoid blocking
- Only sends when game window is valid (`Core.Process.Address != IntPtr.Zero`)

### Cooldown System (Three Levels)

**1. Global key press delay** (`MiscHelper.cs` lines 18, 109-116):
```csharp
DelayBetweenKeys.ElapsedMilliseconds >= Core.GHSettings.KeyPressTimeout + Rand.Next() % 10
```
- Minimum delay between ANY key press
- Configurable + random jitter (anti-detection)

**2. Per-rule cooldown** (`Rule.cs` lines 27, 172-181):
```csharp
cooldownStopwatch.Elapsed.TotalSeconds > delayBetweenRuns
```
- User-configurable 0-30 seconds per rule
- Prevents flask/skill spam

**3. Wait component** (`Component/Wait.cs` lines 51-71):
- Condition must stay true for N seconds before triggering
- Example: wait 0.5s after HP drops before pressing defense flask

### Pre-execution Safety Checks
Before any rules evaluate (`AutoHotKeyTriggerCore.cs` lines 303-369):
1. Game state is InGameState (not menu/loading)
2. Player not in town (configurable)
3. Player alive (Health > 0)
4. Player not in grace period
5. Chat window not open

### Configuration Persistence
- JSON format with `TypeNameHandling.Auto` for polymorphic conditions
- Saved to `config/settings.txt` in plugin directory
- Loads on enable, saves on change

## Implementation Plan for POE2Radar

### What We Already Have
- Key sender: `SendInputNative.Tap()` (scancode-based, better than SendMessage)
- HP/Mana reading: `Poe2Live.PlayerVitals()`
- Entity proximity: `Poe2Live.Entities()` with grid distance
- Web dashboard for configuration
- Settings persistence (JSON)

### v1 — Simple Rule Engine (~300 lines)
Conditions supported without new offset work:
- HP/Mana percent thresholds ✓
- Nearby enemy count by rarity ✓
- Cooldown timers ✓
- Key-held check (GetAsyncKeyState) ✓
- In-game check ✓

Architecture:
- `Core/Automation/AutoRule.cs` — rule data model (key, conditions, cooldown)
- `Core/Automation/RuleEngine.cs` — evaluate rules each tick, send keys
- `Web/` — Rules tab in dashboard for configuration
- `config/auto_rules.json` — persistence

### v2 — Full Feature (Needs CE/IDA Work)
Additional conditions requiring new game memory offsets:
- **Buff/debuff reading** — need Buffs component offsets (entity → component lookup → "Buffs" → buff list)
- **Flask state** — need ServerData offsets (flask inventory, charges, active effects)
- **Skill cooldowns** — need Actor component offsets (ActiveSkills list, IsSkillUsable set)
- **Animation state** — need Actor.Animation offset

### Offset Work Needed for v2
| Component | What to find | Approach |
|-----------|-------------|----------|
| Buffs | Entity → Buffs component → buff array | CE: scan for known buff string, trace back |
| Flask inventory | ServerData → FlaskInventory → flask slots | CE: find flask charge value, pointer scan |
| Actor/Skills | Entity → Actor component → ActiveSkills | CE: scan for skill ID, trace component |
| Animation | Actor → Animation int field | CE: watch value change during attack/cast |

## ExileForge / TF Tool
**Status: Binary only, no source available.**
Files at `C:\Tools\TF\captured\` — packed/VMProtect'd DLLs.
Would need Ghidra/IDA decompilation to analyze. Not feasible without significant RE effort.

## Key Source Files Reference
| File | Purpose | Lines |
|------|---------|-------|
| `AutoHotKeyTriggerCore.cs` | Main plugin loop, rule execution | 145-246 |
| `Rule.cs` | Rule definition, evaluation, cooldown | 104-114, 172-181 |
| `DynamicCondition.cs` | Condition evaluation, Linq parser | 85-89, 127-139 |
| `DynamicConditionState.cs` | All game state for conditions | 18-122 |
| `MiscHelper.cs` | SendMessage key press, global cooldown | 97-125 |
| `AutoHotKeyTriggerSettings.cs` | Profile/config structure | 14-65 |
| `Component/Wait.cs` | Timed condition delay | 51-71 |
| `ProfileManager/Enums/ConditionType.cs` | All condition type definitions | 11-56 |
| `DynamicConditions/FlaskInfo.cs` | Flask state model | 15-69 |
| `DynamicConditions/NearbyMonsterInfo.cs` | Monster proximity tracking | 13-123 |
