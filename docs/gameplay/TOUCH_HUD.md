# Touch HUD (in-match controls)

The Android/iOS combat HUD is **100 % code-painted** - the build ships zero HUD
images. `HudArtFactory` paints every sprite pixel-by-pixel at runtime in the
neon "glass orb" style: smoked-glass disc, diagonal gloss sweep, inner rim
light and 1-3 concentric cyan glow rings, with white pictograms on top.

## Controls (1920x1080 reference, ScaleWithScreenSize)

| Control | Widget | Art | Position (anchor, px) |
| --- | --- | --- | --- |
| Move joystick | `MoveStick` + `Knob` | glass disc + neon ring knob | left-bottom (320, 300), 300 px |
| Look joystick | `LookStick` | same, 45 % alpha | right-middle (-260, 210), 220 px |
| Fire (hold) | `FireButton` | 1-ring orb + bullet pictogram | (-250, 210), 220 px |
| Jump | `JumpButton` | 1-ring orb + hoop-runner pictogram | (-250, 455), 150 px |
| Aim (hold) | `AimButton` | 1-ring orb + crosshair pictogram | (-465, 455), 140 px |
| Sprint (hold) | `SprintButton` | 3-ring orb + runner pictogram | (-500, 265), 140 px |
| Crouch (hold) | `CrouchButton` | 2-ring orb + kneeling pictogram | (-465, 95), 140 px |

Press feedback is a cyan flash (`pressedColor`), not the default grey dim.
The invisible `LookArea` sits behind everything so drags on empty screen turn
the camera while drags on orbs hit their control.

## Control semantics (playtest-locked)

* **Crouch button** = tap to SIT, tap again to stand. The seated pose is stable:
  the PlayerInputRouter is the single per-frame writer of motor input, so the
  crouch state can no longer flicker stand/sit ("up-down") frame to frame.
* **Sprint button** = tap to RUN (toggle, cyan-green orb tint while on), tap
  again to walk. Jump leaves the crouch.
* **Move joystick** = WALK only. Full stick deflection no longer auto-sprints;
  running happens exclusively through the sprint button (or shift / gamepad
  stick-button on desktop).

Tests: `ControlSemanticsTests` locks sit-stays-seated over 60 frames,
sprint-stays-on while toggled, full-stick = walk, jump stands up.

## Wiring

* `MobileControlsBuilder.Build` creates the canvas and returns the `Rig`.
* Hold buttons feed `PlayerInputRouter.SetFireHeld/SetAimHeld/SetSprintHeld/
  SetCrouchHeld`; with a `PlayerController` present the UI buttons call its
  `OnJumpButtonClicked / OnCrouchButtonClicked / OnSprintButtonClicked`.
* Sticks are `VirtualJoystick` components configured into the router.

## Sprite cache

`HudArtFactory` caches each painted sprite (`BH_HUD_*`) so the whole cluster
costs a handful of 256 px RGBA32 textures once per session.

Tests: `TouchHudArtTests` (sprites are code-painted `BH_HUD_*`, icons present,
thumb cluster does not overlap).
