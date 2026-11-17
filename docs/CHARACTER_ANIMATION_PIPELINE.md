# Character & Animation Import Pipeline

How uploaded characters and animations become playable in **Battle Horizons:
Last Survive** — from FBX on disk to a generated Animator Controller driving
the player in the actual game.

## Where assets live

```text
Assets/Characters/{Male,Female,<Custom>}/
    <Name>.fbx           imported character (source copy — never modified)
    Materials/           URP materials created for the model's slots
    Textures/            extracted embedded + copied PNG/JPG/TGA textures
Assets/Animations/
    Movement/            Idle, Walk, Run, Sprint, Jump, Fall, strafe/turn
    Combat/              Aim, Melee, hit reactions, grenade toss
    Weapon/              Rifle Idle, Rifle Run, Shoot, Reload, Weapon Switch
    Crouch/              Crouch, Crouch Walk, prone, crouch variants
    Death/               Death clips (one-shot)
Assets/AnimationControllers/<character>_Locomotion.controller
Assets/AvatarMasks/UpperBody.mask
Assets/Generated/       config + import reports (git-ignored, code-generated)
```

`Assets/UserAssets/Models/Animation/` keeps only `showcase_all.glb` and a
`.gitkeep` — the animation library itself is organized under
`Assets/Animations/`.

## Authoring loop (editor window)

**Battle Horizons → Asset Management → Character & Animation Manager**

1. **Character tab** — pick Male / Female / custom slot, choose the FBX
   (optionally a folder of external PNG/JPG/TGA textures), import. The
   pipeline copies the FBX, configures the rig (Humanoid with a
   convention-mapping fallback for Unreal-style skeletons — `pelvis`,
   `spine_01…03`, `upperarm_l`, `clavicle_l`, `index_01_l`, …), extracts
   embedded textures, builds URP/Lit materials and remaps them onto the
   model's slots (assignments preserved, missing textures warned). Existing
   files are only replaced after explicit confirmation (never in batch mode).
   A **Generic rig** escape hatch is available for rigs that cannot map.
2. **Animations tab** — multi-select animation FBX files. Each file is
   inspected (duration, frame rate, loop, humanoid/generic, retargetability)
   and classified into a category from its file name — overridable per file
   before import. Duplicate content (SHA-256) is skipped. "Scan existing
   Assets/Animations" registers clips that were moved into the library.
3. **Assign tab** — character → category → clip, with compatibility checks
   (a clip is refused with an explanation when it is generic motion or the
   character's avatar is not a valid human avatar — nothing is ever forced).
   **Save** persists `Assets/Generated/Resources/CharacterAnimationConfig.asset`
   and rebuilds that character's Animator Controller.
4. **Preview tab** — real-time 3D preview: rotate (drag), zoom (scroll),
   play/pause, switch character/clip, with rig and material status.
5. **Report tab** — success / warning / failed lines for the last run;
   saved to `Assets/Generated/LastImportReport.md`.

The same preview runs in-game: the **Character** hotspot on the Home Screen
opens the *Character Bay* modal (existing UI, no external dependencies).

## Generated Animator Controllers

Parameters: `Speed`, `VerticalSpeed`, `IsAirborne`, `IsCrouching`, `IsAiming`,
`Shoot`, `Reload`, `Death`, `WeaponSwitch`, `Melee`.

Base layer states: `Idle`, `Walk`, `Run`, `Sprint` (Speed zone transitions,
no exit time, 0.12 s smooth damping), `Crouch` (1D blend Crouch ↔ Crouch Walk
by `Speed`), `Jump`/`Fall` (IsAirborne + VerticalSpeed sign), `Aim` (grounded,
slow, not crouched), one-shots `Shoot`/`Reload`/`WeaponSwitch`/`Melee`
(trigger → Idle after exit time), `Death` (absorbing).

Second layer `UpperBody` (default weight 0, damped by gameplay aiming) with
`Assets/AvatarMasks/UpperBody.mask` (root/legs off, torso/arms/head on) and
states `RifleIdle`/`RifleRun`. `writeDefaultValues` is off on that layer so
the mask controls what it writes.

Missing clips skip the related state with a warning; an assigned **Idle** is
required or the build fails with a clear message. No animation events are
used — none are required by this setup.

## Runtime integration

* `PlayerCharacterVisual` (on the player, created by
  `ProjectBootstrap.PopulatePlayableIslandScene`) instantiates the selected
  character's model, disables the capsule visual, assigns the generated
  controller, and drives parameters from `ThirdPersonMotor`
  (`HorizontalSpeed` → `Speed`, grounded/velocity → airborne params,
  crouch flag, damped UpperBody weight while aiming). With no configured
  character it stays dormant — the capsule visual and all gameplay behave
  exactly as before.
* `CharacterPreviewController` renders the character into a `RenderTexture`
  (camera + light owned by the component; `Tick`/`RenderOnce` support manual
  rendering in edit mode).

## Validation & tests

* EditMode `CharacterPipelineTests`: real Male/Female avatars must be valid
  humanoids (≥40 mapped bones), the moved animation library must import as
  humanoid clips, category inference is checked against the real file names,
  a full character upload (from a temp copy of the real Male FBX) must
  extract textures, build URP materials and create external-object remaps,
  duplicate content must be detected and unconfirmed overwrites refused, and
  controller generation must produce all 10 parameters, the expected states
  and the UpperBody layer with its mask.
* PlayMode `CharacterPreviewRuntimeTests`: preview texture lifecycle and
  navigation APIs, Character Bay opens from the Home Screen hotspot, and the
  player visual spawns the configured character (or honestly stays a capsule).
* CI (`unity.yml`): structure, C# static analysis, island-data integrity
  (with LFS), Blender pipeline tests, then the real Unity 6000.0.85f2
  EditMode → PlayMode test runs and an Android build entry point.

## Android budget

Character textures follow the project import rules (≤512², mipmaps on,
ASTC 6×6 on Android, aniso off); meshes import optimized
(`optimizeMeshPolygons/Vertices`, welded, 4 bone influences, readable off);
animation compression is `Optimal`; materials reuse the URP/Lit shader
(metallic 0, smoothness 0.3); the runtime visual adds no per-frame
allocations (all state is cached).

## Known limitations

* Female character textures reference external Dropbox paths in the source
  FBX — extraction reports them as missing-texture warnings; drop the files
  into `Assets/Characters/Female/Textures/` (or use the external-texture
  folder option) and reimport.
* Unity's built-in humanoid auto-mapper is used first; when it cannot map an
  unusual skeleton the pipeline retries with its convention table, and if
  that fails too the import reports the missing core bones and offers the
  explicit Generic option instead of forcing an incompatible rig.
* PlayMode runs that execute before the EditMode pass (standalone runs
  without a generated config) skip character-specific assertions honestly
  instead of faking them.
