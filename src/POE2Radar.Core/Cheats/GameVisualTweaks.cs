using POE2Radar.Core.Game;
using POE2Radar.Core.Native;

namespace POE2Radar.Core.Cheats;

public sealed class GameVisualTweaks
{
    private readonly ProcessHandle _process;
    private readonly MemoryReader _reader;
    private readonly Poe2Live _live;
    private readonly HashSet<nint> _applied = new();
    private nint _cacheKey;

    public GameVisualTweaks(ProcessHandle process, MemoryReader reader, Poe2Live live)
    {
        _process = process;
        _reader = reader;
        _live = live;
    }

    // ── Render Component Tweaks (visual) ──
    public bool HideNormalLifeBars { get; set; }
    public bool HideMagicLifeBars { get; set; }
    public bool HideAllLifeBars { get; set; }
    public bool HideBuffVisuals { get; set; }
    public bool HideNormalRendering { get; set; }
    public bool ForceShowHover { get; set; }
    public bool DisableSelectionBoxes { get; set; }
    public bool HideInfoDisplay { get; set; }
    public bool HideTalismanIcons { get; set; }
    public bool ForceOutline { get; set; }

    // ── Positioned Component Tweaks (physics) ──
    public bool DisableMonsterBlocking { get; set; }
    public bool DisableMonsterPush { get; set; }
    public bool EnablePhaseThrough { get; set; }

    // ── Targetable Component Tweaks ──
    public bool ForceAllTargetable { get; set; }
    public bool ForceAllAttackable { get; set; }

    // ── Pathfinding Tweaks ──
    public bool FreezeNormalMonsters { get; set; }

    // ── Life Component Tweaks ──
    public bool PreventCorpseSinking { get; set; }

    // ── Impactful tweaks ──
    public float EntityColorR { get; set; } = -1f;
    public float EntityColorG { get; set; } = -1f;
    public float EntityColorB { get; set; } = -1f;
    public float EntityScale { get; set; }
    public bool SwapTeamToFriendly { get; set; }
    public bool InstantTransitions { get; set; }
    public bool UnblockDoors { get; set; }
    public bool UnlockChests { get; set; }
    public bool OpenChestsOnDamage { get; set; }
    public bool MakeAllBoss { get; set; }
    public bool RemoveBossFlag { get; set; }
    public float LabelViewDistance { get; set; }

    // ── DevTest ──
    public bool DevHideHover { get; set; }
    public bool DevFadeArrows { get; set; }
    public bool DevDisableLight { get; set; }
    public bool DevFixedSelectionSize { get; set; }
    public bool DevBBoxIgnoreGround { get; set; }
    public bool DevFaceWindDirection { get; set; }
    public bool DevDampenHeight { get; set; }
    public float DevHeightOffset { get; set; }
    public float DevSelectionHeightOverride { get; set; }
    public bool DevLockOrientation { get; set; }
    public bool DevMakeFlying { get; set; }
    public bool DevMakeStatic { get; set; }
    public bool DevFaceMovementDir { get; set; }
    public bool DevAvoidOthers { get; set; }
    public bool DevLockAnimation { get; set; }
    public bool DevCorpseUsable { get; set; }
    public bool DevNoCorpseMarker { get; set; }

    // ── Apply scope ──
    public bool ApplyToNpcs { get; set; }
    public bool ApplyToChests { get; set; }

    public int Applied => _applied.Count;

    public bool AnyEnabled =>
        HideNormalLifeBars || HideMagicLifeBars || HideAllLifeBars ||
        HideBuffVisuals || HideNormalRendering || ForceShowHover ||
        DisableSelectionBoxes || HideInfoDisplay || HideTalismanIcons || ForceOutline ||
        DisableMonsterBlocking || DisableMonsterPush || EnablePhaseThrough ||
        ForceAllTargetable || ForceAllAttackable ||
        FreezeNormalMonsters || PreventCorpseSinking ||
        EntityColorR >= 0 || EntityScale > 0 || SwapTeamToFriendly ||
        InstantTransitions || UnblockDoors || UnlockChests || OpenChestsOnDamage ||
        MakeAllBoss || RemoveBossFlag || LabelViewDistance > 0 ||
        DevHideHover || DevFadeArrows || DevDisableLight || DevFixedSelectionSize ||
        DevBBoxIgnoreGround || DevFaceWindDirection || DevDampenHeight ||
        DevHeightOffset != 0 || DevSelectionHeightOverride != 0 ||
        DevLockOrientation || DevMakeFlying || DevMakeStatic || DevFaceMovementDir ||
        DevAvoidOthers || DevLockAnimation || DevCorpseUsable || DevNoCorpseMarker;

    public void Tick(nint areaInstance, IReadOnlyList<Poe2Live.EntityDot> entities)
    {
        if (areaInstance != _cacheKey) { _applied.Clear(); _cacheKey = areaInstance; }
        if (!AnyEnabled) return;

        foreach (var e in entities)
        {
            if (_applied.Contains(e.Address)) continue;

            var isMonster = e.Category == Poe2Live.EntityCategory.Monster;
            var isNpc = e.Category == Poe2Live.EntityCategory.Npc;
            var isChest = e.Category == Poe2Live.EntityCategory.Chest;
            var isTransition = e.Category == Poe2Live.EntityCategory.Transition;
            var isObject = e.Category == Poe2Live.EntityCategory.Object;
            if (!isMonster && !isTransition && !(isNpc && ApplyToNpcs) && !(isChest && (ApplyToChests || UnlockChests || OpenChestsOnDamage)) && !UnblockDoors) continue;

            bool wrote = false;

            // ── Render writes (monsters + optionally NPCs) ──
            if (isMonster || (isNpc && ApplyToNpcs))
            {
                var render = _live.ResolveComponentAddress(e.Address, "Render");
                if (render != 0)
                {
                    if (HideAllLifeBars)
                        wrote |= WriteByte(render + Poe2.Render.HideMiniLifeBar, 1);
                    else if (HideMagicLifeBars && e.Rarity is Poe2Live.Rarity.Normal or Poe2Live.Rarity.Magic)
                        wrote |= WriteByte(render + Poe2.Render.HideMiniLifeBar, 1);
                    else if (HideNormalLifeBars && e.Rarity == Poe2Live.Rarity.Normal)
                        wrote |= WriteByte(render + Poe2.Render.HideMiniLifeBar, 1);

                    if (HideBuffVisuals) wrote |= WriteByte(render + Poe2.Render.HideAllBuffVisuals, 1);
                    if (HideNormalRendering && e.Rarity == Poe2Live.Rarity.Normal && !e.IsBoss) wrote |= WriteByte(render + Poe2.Render.DisableRendering, 1);
                    if (ForceShowHover) wrote |= WriteByte(render + Poe2.Render.AlwaysShowHover, 1);
                    if (DisableSelectionBoxes && e.Rarity == Poe2Live.Rarity.Normal) wrote |= WriteByte(render + Poe2.Render.NoSelectionBox, 1);
                    if (HideInfoDisplay) wrote |= WriteByte(render + Poe2.Render.HideInfoDisplay, 1);
                    if (HideTalismanIcons) wrote |= WriteByte(render + Poe2.Render.HideTalismanIcon, 1);
                    if (ForceOutline) wrote |= WriteByte(render + Poe2.Render.ForceOutline, 1);

                    // Impactful
                    if (EntityColorR >= 0) { WriteFloat(render + 0x58, EntityColorR); WriteFloat(render + 0x5C, EntityColorG); WriteFloat(render + 0x60, EntityColorB); wrote = true; }
                    if (LabelViewDistance > 0) { WriteFloat(render + 0xF8, LabelViewDistance); wrote = true; }

                    // DevTest render
                    if (DevHideHover) wrote |= WriteByte(render + 0x78, 1);
                    if (DevFadeArrows) wrote |= WriteByte(render + 0x75, 1);
                    if (DevDisableLight) wrote |= WriteByte(render + 0x71, 1);
                    if (DevFixedSelectionSize) wrote |= WriteByte(render + 0x80, 1);
                    if (DevBBoxIgnoreGround) wrote |= WriteByte(render + 0x7F, 1);
                    if (DevFaceWindDirection) wrote |= WriteByte(render + 0x86, 1);
                    if (DevDampenHeight) wrote |= WriteByte(render + 0x72, 1);
                    if (DevHeightOffset != 0) { WriteFloat(render + 0xF4, DevHeightOffset); wrote = true; }
                    if (DevSelectionHeightOverride != 0) { WriteFloat(render + 0x110, DevSelectionHeightOverride); wrote = true; }
                }
            }

            // ── Positioned writes ──
            if (isMonster || (isNpc && ApplyToNpcs))
            {
                var pos = _live.ResolveComponentAddress(e.Address, "Positioned");
                if (pos != 0)
                {
                    if (DisableMonsterBlocking) wrote |= WriteByte(pos + Poe2.Positioned.Blocking, 0);
                    if (DisableMonsterPush) { wrote |= WriteByte(pos + Poe2.Positioned.DoesNotPushWhenPushed, 1); wrote |= WriteByte(pos + Poe2.Positioned.IgnoreBeingPushed, 1); }
                    if (EnablePhaseThrough) wrote |= WriteByte(pos + Poe2.Positioned.PhaseThrough, 1);
                    if (SwapTeamToFriendly) { WriteShort(pos + Poe2.Positioned.Team, 1); wrote = true; }
                    if (EntityScale > 0) { WriteFloat(pos + Poe2.Positioned.Scale, EntityScale); wrote = true; }

                    // DevTest positioned
                    if (DevLockOrientation) wrote |= WriteByte(pos + 0x18, 1);
                    if (DevMakeFlying) wrote |= WriteByte(pos + 0x2B, 1);
                    if (DevMakeStatic) wrote |= WriteByte(pos + 0x20, 1);
                    if (DevFaceMovementDir) wrote |= WriteByte(pos + 0x2D, 1);
                }
            }

            // ── Targetable writes ──
            if ((isMonster || isNpc || isChest) && (ForceAllTargetable || ForceAllAttackable))
            {
                var tgt = _live.ResolveComponentAddress(e.Address, "Targetable");
                if (tgt != 0)
                {
                    if (ForceAllTargetable) wrote |= WriteByte(tgt + Poe2.Targetable.IsTargetable, 1);
                    if (ForceAllAttackable) wrote |= WriteByte(tgt + Poe2.Targetable.Attackable, 1);
                }
            }

            // ── Pathfinding writes ──
            if (isMonster)
            {
                var pf = _live.ResolveComponentAddress(e.Address, "Pathfinding");
                if (pf != 0)
                {
                    if (FreezeNormalMonsters && e.Rarity == Poe2Live.Rarity.Normal && !e.IsBoss) wrote |= WriteInt(pf + Poe2.PathfindingComponent.BaseSpeed, 0);
                    if (DevAvoidOthers) wrote |= WriteByte(pf + 0xD0, 0);
                }
            }

            // ── Life writes ──
            if (isMonster)
            {
                var life = _live.ResolveComponentAddress(e.Address, "Life");
                if (life != 0)
                {
                    // ⚠ These offsets (IDA-sourced) need revalidation after the 2026-06-04 vital-block shift
                    if (PreventCorpseSinking && !e.IsAlive) wrote |= WriteByte(life + 0xE4, 1);
                    if (DevCorpseUsable) wrote |= WriteByte(life + 0xE7, 1);
                    if (DevNoCorpseMarker) wrote |= WriteByte(life + 0xE5, 1);
                }
            }

            // ── Monster component writes ──
            if (isMonster && (MakeAllBoss || RemoveBossFlag))
            {
                var mon = _live.ResolveComponentAddress(e.Address, "Monster");
                if (mon != 0)
                {
                    if (MakeAllBoss) wrote |= WriteByte(mon + Poe2.MonsterComponent.IsBoss, 1);
                    if (RemoveBossFlag) wrote |= WriteByte(mon + Poe2.MonsterComponent.IsBoss, 0);
                }
            }

            // ── Animated writes ──
            if (isMonster && DevLockAnimation)
            {
                var anim = _live.ResolveComponentAddress(e.Address, "Animated");
                if (anim != 0) wrote |= WriteByte(anim + 0xC0, 1);
            }

            // ── AreaTransition writes (instant transitions) ──
            if (isTransition && InstantTransitions)
            {
                var at = _live.ResolveComponentAddress(e.Address, "AreaTransition");
                if (at != 0) { WriteFloat(at + 0x1C, 0f); WriteFloat(at + 0x18, 0f); wrote = true; }
            }

            // ── TriggerableBlockage writes (unblock doors) ──
            if (UnblockDoors && (isObject || isTransition || e.Metadata.Contains("Blockage", StringComparison.OrdinalIgnoreCase) || e.Metadata.Contains("Door", StringComparison.OrdinalIgnoreCase)))
            {
                var tb = _live.ResolveComponentAddress(e.Address, "TriggerableBlockage");
                if (tb != 0) { wrote |= WriteByte(tb + 0x10, 0); }
            }

            // ── Chest writes ──
            if (isChest)
            {
                var chest = _live.ResolveComponentAddress(e.Address, "Chest");
                if (chest != 0)
                {
                    if (UnlockChests) wrote |= WriteByte(chest + 0x25, 0);
                    if (OpenChestsOnDamage) wrote |= WriteByte(chest + 0x24, 1);
                }
            }

            if (wrote) _applied.Add(e.Address);
        }
    }

    private bool WriteByte(nint address, byte value)
    {
        var handle = _process.Handle;
        if (!NativeMethods.VirtualProtectEx(handle, address, 1, NativeMethods.PAGE_EXECUTE_READWRITE, out var oldProtect))
            return false;
        bool ok;
        unsafe
        {
            ok = NativeMethods.WriteProcessMemory(handle, address, &value, 1, out var written);
            if (ok && (int)written != 1) ok = false;
        }
        NativeMethods.VirtualProtectEx(handle, address, 1, oldProtect, out _);
        return ok;
    }

    private bool WriteFloat(nint address, float value)
    {
        var handle = _process.Handle;
        if (!NativeMethods.VirtualProtectEx(handle, address, 4, NativeMethods.PAGE_EXECUTE_READWRITE, out var oldProtect))
            return false;
        bool ok;
        unsafe
        {
            ok = NativeMethods.WriteProcessMemory(handle, address, (byte*)&value, 4, out var written);
            if (ok && (int)written != 4) ok = false;
        }
        NativeMethods.VirtualProtectEx(handle, address, 4, oldProtect, out _);
        return ok;
    }

    private bool WriteShort(nint address, short value)
    {
        var handle = _process.Handle;
        if (!NativeMethods.VirtualProtectEx(handle, address, 2, NativeMethods.PAGE_EXECUTE_READWRITE, out var oldProtect))
            return false;
        bool ok;
        unsafe
        {
            ok = NativeMethods.WriteProcessMemory(handle, address, (byte*)&value, 2, out var written);
            if (ok && (int)written != 2) ok = false;
        }
        NativeMethods.VirtualProtectEx(handle, address, 2, oldProtect, out _);
        return ok;
    }

    private bool WriteInt(nint address, int value)
    {
        var handle = _process.Handle;
        if (!NativeMethods.VirtualProtectEx(handle, address, 4, NativeMethods.PAGE_EXECUTE_READWRITE, out var oldProtect))
            return false;
        bool ok;
        unsafe
        {
            ok = NativeMethods.WriteProcessMemory(handle, address, (byte*)&value, 4, out var written);
            if (ok && (int)written != 4) ok = false;
        }
        NativeMethods.VirtualProtectEx(handle, address, 4, oldProtect, out _);
        return ok;
    }
}
