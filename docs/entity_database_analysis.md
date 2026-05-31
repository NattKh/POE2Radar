# Entity Database Analysis

6,692 entity paths from the GGPK, analyzed 2026-05-30.

## Top-Level Categories

| Category | Count | % | Description |
|----------|-------|---|-------------|
| monsters | 4,698 | 70.2% | Everything that fights |
| npc | 749 | 11.2% | NPCs and quest givers |
| miscellaneousobjects | 671 | 10.0% | Waypoints, transitions, stash, portals |
| chests | 365 | 5.5% | Loot containers |
| characters | 252 | 3.8% | Player classes, cosmetics |
| shrines | 107 | 1.6% | All shrine types |
| critters | 43 | 0.6% | Ambient wildlife |
| questobjects | 31 | 0.5% | Quest interactables |

## Useful Sub-Categories

### Quest / Navigation
| Pattern | Count | Use |
|---------|-------|-----|
| MiscellaneousObjects/Waypoint | 14 | All waypoint variants |
| MiscellaneousObjects/Checkpoint | 21 | All checkpoint variants |
| MiscellaneousObjects/AreaTransition | 15 | Zone exits/entrances |
| MiscellaneousObjects/Stash | 88 | Stash (many skins) |
| MiscellaneousObjects/TownPortal | 5 | Portal variants |
| QuestObjects | 31 | Quest interactables |
| QuestChests | 12 | Quest reward chests |
| EinharQuestMarker | 1 | Quest markers |

### Combat / Bosses
| Pattern | Count | Use |
|---------|-------|-----|
| boss (in path) | 72 | Boss encounters |
| BossRoomMinimapIcon | 41 | Boss room markers |
| Strongbox | 64 | Strongbox monsters |
| Shrine (under monsters/daemon) | 107 | Shrine effects |

### League Mechanics
| Pattern | Count | Use |
|---------|-------|-----|
| LeagueRitual | 51 | Ritual encounters |
| LeagueExpedition / Expedition2 | 107+185 | Expedition encounters |
| LeagueAbyss / Abyss | 177+65 | Abyss encounters |
| Breach | 125 | Breach encounters |
| LeagueDelirium | 75 | Delirium encounters |
| LeagueIncursionNew | 310 | Incursion (temples) |
| LeagueAncestral | 71 | Ancestral encounters |
| LeagueUltimatum | 47 | Ultimatum encounters |
| LeagueHellscape | 45 | Hellscape encounters |
| MarakethSanctumTrial | 80 | Sanctum trials |

### NPCs by Act
| Pattern | Count | Use |
|---------|-------|-----|
| NPC/Four_Act1 | 46 | Act 1 NPCs |
| NPC/Four_Act2 | 71 | Act 2 NPCs |
| NPC/Four_Act3 | 65 | Act 3 NPCs |
| NPC/Four_Act4 | 129 | Act 4 NPCs |
| NPC/Four_Interlude | 39 | Interlude NPCs |
| NPC/Four_Endgame | 38 | Endgame NPCs |
| Monsters/NPC (generic) | 52 | In-zone NPCs |

### Crafting / Town
| Pattern | Count | Use |
|---------|-------|-----|
| ReforgingBench | 1 | Reforging |
| TransmutationBench | 2 | Crafting |
| VeisiumCraftingBench | 1 | Crafting |
| GuildStash | varies | Guild stash |

## Junk Categories (hide from radar/database)

These clutter search results and the radar with no gameplay value:

| Pattern | Count | Why junk |
|---------|-------|----------|
| attachments | 438 | Visual attachment points on models |
| monstermods | 194 | Invisible modifier daemons |
| microtransactions | 53 | MTX cosmetics |
| timelines | 191 | Player cosmetic timelines |
| stashskins | 60 | Stash visual variants |
| fx | 19+ | Particle effects |
| mat | 15+ | Material definitions |
| ao | 13+ | Ambient occlusion data |
| epk | 14+ | Effect packages |
| graph | 15 | Skill graph nodes |
| audio | 17 | Sound definitions |
| pet | 14 | Pet cosmetics |
| clone | 36 | Player clone effects |
| playersummoned | 41 | Summoned entity base classes |
| environment | 48 | Environment settings |
| essencemoddaemons | 44 | Invisible essence modifiers |
| tormentedspirits | 48 | Tormented spirit daemons |
| daemon (generic) | 38+ | Invisible helper entities |
| bossroomminimapicon | 41 | Minimap icon entities (already handled by POI) |

## Junk Filter Regex

For hiding junk in the database search and radar:
```
attachments|monstermods|microtransactions|timelines|stashskins|/fx/|/mat/|/ao/|/epk/|/graph/|/audio/|/pet/|clone|playersummoned|essencemoddaemons|tormentedspirits|/daemon/|bossroomminimapicon|environment|hairstyles|outfits|weapons/|runemarked
```

## Monster Sub-Categories (for radar filtering)

| Sub-category | Count | Examples |
|-------------|-------|---------|
| vaalmonsters | 148 | Vaal constructs, zealots |
| goblins | 67 | Goblin variants |
| skeletons | 61 | Skeleton soldiers |
| zombies | 41 | Zombie types |
| bloodfeverkarui | 41 | Blood fever tribe |
| twilightorder | 87 | Twilight order soldiers |
| breach (monsters) | 117 | Breach monsters |
| leagueabyss (monsters) | 98 | Abyss monsters |
| cultureshrines | 84 | Culture shrine daemons |
| settlers/peasants | 91 | Kalguur settlers |
| totems | 39 | Totem entities |
| mutewind | 36 | Mutewind tribe |
