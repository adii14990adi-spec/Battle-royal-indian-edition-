# export_presets/

`export_presets.cfg` at the repository root is the **source of truth**: it is the
file the Godot editor reads and writes, the file CI exports with, and the file
the Phase 1 acceptance gate greps (`permissions/internet=false`, `export_format=0`).

This folder holds **read-only snapshots** of that file, one per phase tag, so
preset drift is auditable in review:

```
export_presets/
└── android_apk_unisoc.v0.0.1-setup.cfg
```

Rules:

- Never edit a snapshot; edit the root file and archive a new copy at the next
  phase tag (`android_apk_unisoc.<tag>.cfg`).
- Keystore paths and passwords are **not** in `export_presets.cfg` — Godot writes
  `keystore/*` options to `export_credentials.cfg`, which must never be committed
  (see `CONTRIBUTING.md`).
- Re-take a snapshot with:

  ```bash
  cp export_presets.cfg export_presets/android_apk_unisoc.$(git describe --tags --abbrev=0).cfg
  ```
