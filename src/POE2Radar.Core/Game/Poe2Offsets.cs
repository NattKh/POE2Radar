namespace POE2Radar.Core.Game;

/// <summary>
/// PoE2 memory offsets — the going-forward source of truth, sourced from the GameHelper2
/// <c>GameOffsets/</c> dump and validated against the live client where marked ✓.
///
/// <para>This is separate from the legacy PoE1-shaped <see cref="KnownOffsets"/> (which the
/// overlay still references and which is being migrated). As each PoE2 structure is validated
/// here, the corresponding overlay reader is rechained to use it.</para>
///
/// Markers: ✓ = confirmed against live PoE2; (GH2) = from GameHelper2, not yet live-checked.
/// </summary>
public static class Poe2
{
    /// <summary>Tile→world = 250, tile→grid = 23 ⇒ world/grid ratio ≈ 10.8696. ✓</summary>
    public const float WorldToGridRatio = 250f / 23f;

    /// <summary>Conservative network-bubble radius in grid units (GH2 uses 150). </summary>
    public const int NetworkBubbleGrid = 150;

    /// <summary>
    /// GameState root — found via the "Game States" AOB pattern (<see cref="AobPatterns"/>).
    /// Holds the array of game-state slots; one of them is InGameState.
    /// </summary>
    public static class GameState
    {
        public const int CurrentStatePtr = 0x08;  // (GH2) StdVector — current state
        public const int States          = 0x48;  // (GH2) inline array of 12 × StdTuple2D<IntPtr> (16 bytes each)
        public const int StateSlotStride = 0x10;   // each slot is StdTuple2D<IntPtr> (ptr + extra)
        public const int StateSlotCount  = 12;
    }

    /// <summary>
    /// InGameState. Resolve it from <c>GameState.CurrentStatePtr</c> (StdVector @ +0x08): the
    /// vector's first element is the active state pointer when in-game. ✓ (matches States[] slot).
    /// </summary>
    public static class InGameState
    {
        public const int AreaInstanceData = 0x290; // ✓ → AreaInstance (validated: target holds the local player)
        public const int UiRoot           = 0x2F0; // ✓ → root UiElement (self-ref; children are UI elements)
        public const int Camera           = 0x368; // ✓ → Camera object (Zoom @ +0x528 == 1.0 confirmed)
        public const int WorldData        = 0x310; // (GH2-drift) → WorldData (area name + camera) — TBD
        public const int UiRootStructPtr  = 0x340; // (GH2-drift) reads 0 here — TBD
    }

    public static class UiRootStruct
    {
        public const int UiRootPtr = 0x5A8; // (GH2)
        public const int GameUiPtr = 0xBF0; // (GH2)
    }

    /// <summary>
    /// The big per-area container: area metadata, player, entity maps, terrain.
    /// <para>⚠ GameHelper2's internal offsets are DRIFTED in this build — confirmed by the live
    /// probe (PlayerInfo moved from GH2's 0xA00 to ~0x580; LocalPlayer at 0x5A0). The values
    /// marked (GH2-drift) below must be re-discovered (see <c>--find-entities</c> / <c>--find-terrain</c>).</para>
    /// </summary>
    public static class AreaInstance
    {
        public const int AreaInfoPtr      = 0x0A0;  // ✓ → AreaInfo; +0x00 → UTF-16 "Code\0Name\0" (Code validated 'G1_town')
        public const int LocalPlayer      = 0x5A0;  // ✓ → player Entity (value-scanned player matched here)
        public const int ServerDataPtr    = 0x580;  // candidate (heap ptr just before the player slot)
        public const int AwakeEntities    = 0x6C0;  // ✓ StdMap of live entities (id→EntityPtr); validated size=378
        public const int SleepingEntities = 0x6D0;  // ✓ StdMap (validated size=58)
        public const int TerrainMetadata  = 0x8A0;  // ✓ TerrainStruct base (GH2's 0xD20 drifted)
        public const int CurrentAreaLevel = 0x0C4;  // ✓ int — per-area, validated 27/32 (GH2's 0xBC drifted)
        public const int CurrentAreaHash  = 0x11C;  // ✓ uint — per-area random hash (GH2's 0xFC drifted; +0x120 paired seed)
    }

    /// <summary>Entity StdMap conventions. Maps live at AreaInstance+0x6C0 (Awake) / +0x6D0 (Sleeping).</summary>
    public static class EntityList
    {
        public const int StdMapSize = 0x10; // each StdMap is {Head ptr, int Size, pad} = 16 bytes
        /// <summary>Entity ids below this are real entities; above are visuals/decorations (GH2 filter). ✓ confirmed live.</summary>
        public const uint VisualIdThreshold = 0x40000000;
    }

    /// <summary>std::map node: Left/Parent/Right ptrs, Color, IsNil byte, then Data{Key,Value} @ +0x20.</summary>
    public static class StdMapNode
    {
        public const int Left   = 0x00;
        public const int Parent = 0x08;
        public const int Right  = 0x10;
        public const int IsNil  = 0x19; // bool
        public const int Data   = 0x20; // Key (EntityNodeKey: uint id + pad = 8 bytes), then Value (IntPtr EntityPtr)
        public const int KeyId  = 0x20; // uint entity id
        public const int ValueEntityPtr = 0x28; // IntPtr
    }

    /// <summary>An Entity object.</summary>
    public static class Entity
    {
        public const int EntityDetailsPtr = 0x08; // ✓ → EntityDetails
        public const int ComponentList    = 0x10; // ✓ StdVector of component pointers (8-byte elems)
        public const int Id               = 0x80; // (GH2) uint  (read 0 for local player — revisit)
        public const int IsValid          = 0x84; // (GH2) byte; valid when bit0 clear
    }

    public static class EntityDetails
    {
        public const int Name              = 0x08; // ✓ StdWString — metadata path (e.g. Metadata/Characters/<Class>/<Variant>)
        public const int ComponentLookUpPtr = 0x28; // ✓ → ComponentLookUp
    }

    /// <summary>ComponentLookUp: a StdBucket of (NamePtr, Index) at +0x28; index → ComponentList[index].</summary>
    public static class ComponentLookUp
    {
        public const int NameAndIndexBucket = 0x28; // ✓ StdBucket; its Data StdVector starts here
        public const int EntryStride        = 0x10; // ✓ {IntPtr NamePtr; int Index; int pad}
    }

    // ── Components (offsets from the component object base) ───────────────────

    /// <summary>Life — ✓ validated live (442/442 HP, 271 mana, 186/186 ES).</summary>
    public static class Life
    {
        public const int Owner        = 0x008; // ComponentHeader.EntityPtr (back-pointer to entity)
        public const int Health       = 0x1A8; // ✓ VitalStruct
        public const int Mana         = 0x1F8; // ✓ VitalStruct
        public const int EnergyShield = 0x230; // ✓ VitalStruct
    }

    /// <summary>VitalStruct — ✓ (Max/Current confirmed). Reuse <see cref="VitalStruct"/> for reads.</summary>
    public static class Vital
    {
        public const int ReservedFlat = 0x10;
        public const int Regen        = 0x28;
        public const int Max          = 0x2C; // ✓
        public const int Current      = 0x30; // ✓
    }

    /// <summary>Render component.</summary>
    public static class Render
    {
        public const int CurrentWorldPosition = 0x138; // ✓ Vector3 (X,Y,Z); grid = XY / WorldToGridRatio
        public const int ModelBounds          = 0x144; // candidate (3 floats right after world pos)
    }

    /// <summary>Player component — character name + level. ✓ validated (name StdWString, level byte 27).</summary>
    public static class PlayerComponent
    {
        public const int Name  = 0x1B0; // ✓ StdWString
        public const int Level = 0x204; // ✓ byte (low byte of a u32 slot)
    }

    /// <summary>Camera object (at InGameState+0x368). Holds the WorldToScreen matrix.</summary>
    public static class Camera
    {
        // The matrix is stored duplicated (two identical 0x40-byte copies back-to-back); the first
        // copy is at +0x1A0. Row-major Matrix4x4; screen = project(world * M). Validated visually.
        public const int WorldToScreenMatrix = 0x1A0;
        public const int Zoom = 0x528; // float, == 1.0 confirmed
    }

    /// <summary>ObjectMagicProperties component — monster/chest rarity.</summary>
    public static class ObjectMagicProperties
    {
        // ✓ validated live across 21 monsters (values 0 and 2 seen). Enum: 0=Normal,1=Magic,2=Rare,3=Unique.
        public const int Rarity = 0x144;
    }

    /// <summary>Chest component. ✓ validated live (opened chest = 0, closed = 1 at +0x168).</summary>
    public static class ChestComponent
    {
        public const int OpenState = 0x168; // 1 = closed/openable, 0 = opened/used
    }

    /// <summary>Positioned component.</summary>
    public static class Positioned
    {
        // ✓ validated live: player (friendly) = 0x01, hostile MastodonBoss = 0x00.
        // GameHelper2 rule: IsFriendly = (Reaction & 0x7F) == 1.
        public const int Reaction = 0x1E0;
    }

    /// <summary>
    /// TerrainStruct (base at AreaInstance+0x8A0). Validated live: TotalTiles (54,48) → 2592 tiles
    /// (matches TileDetails count); walkable grid 685584 bytes; BytesPerRow 621 → cellsPerRow 1242;
    /// grid 1242×1104 = (54×23)×(48×23). PoE2 has FOUR grid layers (0xD0/0xE8/0x100/0x118), so
    /// BytesPerRow sits at 0x130 — not GH2's 0x100.
    /// </summary>
    public static class Terrain
    {
        public const int TotalTiles        = 0x18;  // ✓ StdTuple2D<long> (tilesX, tilesY)
        public const int TileDetailsPtr    = 0x28;  // ✓ StdVector of TileStructure (0x38 bytes)
        public const int GridWalkableData  = 0xD0;  // ✓ StdVector — packed walkable grid bytes
        public const int GridLandscapeData = 0xE8;  // ✓ StdVector
        public const int GridLayer3        = 0x100; // ✓ StdVector (extra PoE2 layer)
        public const int GridLayer4        = 0x118; // ✓ StdVector (extra PoE2 layer)
        public const int BytesPerRow       = 0x130; // ✓ int (621 live) — cellsPerRow = ×2
        public const int TileGridCells     = 23;    // tile = 23×23 grid cells
    }

    /// <summary>One entry in Terrain.TileDetailsPtr (0x38 bytes). ✓ validated (TgtPath gives tile names).</summary>
    public const int TileStructureSize = 0x38;
    public static class TileStructure
    {
        public const int SubTileDetailsPtr = 0x00; // pointer
        public const int TgtFilePtr        = 0x08; // ✓ → TgtFileStruct
        public const int TileHeight        = 0x30; // short
        public const int RotationSelector  = 0x36; // byte
    }

    public static class TgtFileStruct
    {
        public const int TgtPath = 0x08; // ✓ StdWString — full tile .tdt path (e.g. .../Feature/arena_01.tdt)
    }

    // ── Map UI — GH2, not yet live-checked ──
    public static class ImportantUi
    {
        public const int MapParentPtr = 0x738; // (GH2) from UiRoot/GameUi
    }

    public static class MapParent
    {
        public const int LargeMapPtr = 0x50; // (GH2)
        public const int MiniMapPtr  = 0x58; // (GH2)
    }

    /// <summary>
    /// MapUiElement (large map + minimap share this class/vtable). ✓ validated live: exactly two
    /// elements carry DefaultShift=(0,-20) with Zoom=0.5. Struct shape matches GH2 (shifted +0x70):
    /// Shift→DefaultShift = 8, DefaultShift→Zoom = 0x38.
    /// </summary>
    public static class MapUiElement
    {
        public const int Shift        = 0x368; // ✓ StdTuple2D<float>
        public const int DefaultShift = 0x370; // ✓ StdTuple2D<float> (0,-20)
        public const int Zoom         = 0x3A8; // ✓ float (0.5 live)
    }

    /// <summary>UiElement base — ✓ validated live (GH2's offsets drifted: Self 0x30→0x8, Flags 0x1B8→0x180).</summary>
    public static class UiElement
    {
        public const int Self           = 0x08;  // ✓ self pointer
        public const int Children       = 0x10;  // ✓ StdVector of child UiElement pointers
        public const int Flags          = 0x180; // ✓ uint; IsVisibleLocal = bit 0x0B (toggle-diff: 0x2EF1↔0x26F1)
        public const int FlagVisibleBit = 0x0B;  // ✓ visible bit (set when shown)
        // Full visibility is hierarchical: an element is shown iff its own bit 0x0B AND every
        // ancestor's bit are set. Walk Parent up to UiRoot (Parent offset still TBD).
    }
}
