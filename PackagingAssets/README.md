# Log/Item Thrower

Launch tree logs and item stacks with a two-stage throw flow: grab with `T`, then hold/release `T` to charge and launch.

## Controls (Default)
- `T` — Grab target, hold to charge, release to throw
- `R` — Toggle rotation mode while holding
- `G` — Drop currently held object

## Features
- Grab nearby `TreeLog` and `ItemDrop` targets from hover
- Shoulder-carry behavior with manual rotation mode
- Charge-based throws with configurable max charge time and force multiplier
- Stamina-based gameplay for grab, throw, and hold drain
- Custom `Throwing` skill integration (SkillManager)
- Optional EpicMMO strength scaling (soft dependency)
- Log impact direct damage, push force, and configurable AoE splash damage
- Item throws can skip across water surfaces
- Blocks usage while chat/menu is open

## Installation
1. Install BepInEx for Valheim (`denikson-BepInExPack_Valheim`).
2. Place `LogItemThrower.dll` in `BepInEx/plugins/`.

## Configuration
All options are available in BepInEx config and in Configuration Manager (`F1`).

### 1 - General
- `LaunchHotkey` (default: `T`) — Grab/charge/throw key
- `DropHotkey` (default: `G`) — Drop held object
- `RotationHotkey` (default: `R`) — Toggle rotation mode

### 2 - Physics
- `GrabRange` (default: `12.0`) — Max grab distance
- `LogForce` (default: `2000`) — Base launch force for logs
- `ItemForce` (default: `150`) — Base launch force for item stacks
- `LogPushForce` (default: `80`) — Knockback force on hit
- `LogDamageCoefficient` (default: `0.01`) — Damage multiplier from force
- `StrengthCoefficient` (default: `0.002`) — EpicMMO strength scaling
- `SkillForceBonus` (default: `0.25`) — Max force bonus from Throwing skill
- `LogHoldRotationX` (default: `90`)
- `LogHoldRotationZ` (default: `0`)
- `LogAoeRadius` (default: `3`)
- `LogAoeDamageMultiplier` (default: `0.5`)

### 3 - Stamina
- `GrabCostPct` (default: `0.12`)
- `ThrowCostPct` (default: `0.15`)
- `PassiveDrainPct` (default: `0.005`)
- `ActiveDrainPct` (default: `0.015`)

### 4 - Charge
- `MaxChargeTime` (default: `2.0`)
- `MaxChargeMultiplier` (default: `2.0`)
- `MaxChargeStaminaMultiplier` (default: `1.5`)

## Changelog
See `CHANGELOG.md`.