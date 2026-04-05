# Note
Future Codex sessions should read this file before making changes.

## 1. Project identity
SixFold / HeatwaveMayor is a Unity 6 project on macOS. It is a narrative-management game with Yarn-driven dialogue, room/NPC systems, runtime UI generation, and a Codex CLI + uloop workflow.

## 2. Environment and workflow
- Platform: macOS.
- Engine: Unity 6.
- Primary workflow: Codex CLI for repo work, `apply_patch` for edits, uloop for compile, console, hierarchy, screenshots, play mode, and runtime checks.
- Safe verification baseline at handoff time: compile clean and console clean.
- Preferred validation loop after code edits: compile, check console, then do targeted runtime verification instead of broad churn.

## 3. Important completed fixes
- Yarn boot/title flow was stabilized so the title cover and run start flow no longer fight the opening dialogue path.
- Start/retry flow button wiring was fixed in the runtime cover UI.
- The dialogue skip button label/wiring was restored through the dialogue UI layout path.
- Dialogue panel lookup was made inactive-safe so hidden UI does not break presenter/controller startup.
- `ObjectiveBeacon` was moved out of the UI hierarchy and attached under the world parent instead.
- Repeated TMP rewrites were removed from the runtime HUD, NPC prompt UI, and objective beacon path.

## 4. Confirmed performance findings
- The previously confirmed lag sources were redundant text rewrites, not runaway spawning.
- Fixed causes:
  - repeated HUD TMP rewrites
  - repeated NPC prompt TMP rewrites
  - repeated objective beacon TMP rewrites
- `HeatwaveRoomNPCSystem.Awake()` is now instrumented with per-phase timing logs using the `HeatwaveStartup` prefix.
- Verified startup timings on April 5, 2026 before the latest optimization:
  - `InitializeTilePixelsPalette` 28.76 ms
  - `BuildRoomDecorations` 13.90 ms
  - `RebuildHeatwaveMap` 5.91 ms
  - `SpawnAllRoomNpcs` 4.23 ms
  - `Awake.Total` 63.64 ms
- Verified startup timings on April 5, 2026 after the latest optimization:
  - `InitializeTilePixelsPalette` 18.61 ms
  - `BuildRoomDecorations` 5.84 ms
  - `RebuildHeatwaveMap` 5.81 ms
  - `SpawnAllRoomNpcs` 2.20 ms
  - `Awake.Total` 42.71 ms
- Current heaviest measured startup phase is still `InitializeTilePixelsPalette`, but it dropped by 10.15 ms after the optimization.
- Verified cause of that phase cost: `TryInitializeExternalDroughtFloorPalette()` loaded many tile sprites during `Awake()`, and `LoadExternalTileSprite()` previously fell through to raw disk reads plus `Texture2D.LoadImage()` PNG decode when the resources mirror was absent.
- Verified low-risk optimization: in the Unity Editor path, `LoadExternalTileSprite()` now uses `UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>()` before disk I/O, which avoids decoding those PNGs during startup.
- Confirmed non-causes during prior verification:
  - no runaway spawning
  - no dialogue restart loop
  - no objective beacon respawn loop
  - no console log spam
- Current remaining suspect: startup cost inside `HeatwaveRoomNPCSystem.Awake()`.

## 5. Files changed for the performance fixes
- `Assets/Scripts/HeatwaveCityGameController.cs`
  - caches last HUD strings and only rewrites TMP text when values actually change
  - contains title/start/retry flow wiring
- `Assets/Scripts/HeatwaveNpcDialogueUI.cs`
  - guards NPC prompt text updates so the prompt TMP is not rewritten every frame
- `Assets/Scripts/HeatwaveRoomNPCSystem.cs`
  - keeps the objective beacon in world space, not UI space
  - creates the beacon once and updates transform/visibility without respawning
  - logs one concise timing line per startup phase in `Awake()` with the `HeatwaveStartup` prefix
  - uses `AssetDatabase.LoadAssetAtPath<Sprite>()` for external tile sprites in the Unity Editor before falling back to disk I/O
- `Assets/Scripts/DialogueUIController.cs`
  - inactive-safe `DialoguePanel` lookup and skip button label/layout handling

## 6. Verified runtime facts
- `uloop` compile returned `ErrorCount: 0` and `WarningCount: 0` on April 5, 2026.
- Unity console was cleared, then read back as empty (`TotalCount: 0`) on April 5, 2026.
- After the latest `HeatwaveRoomNPCSystem` startup pass on April 5, 2026, Unity console checks still showed `ErrorCount: 0` and `WarningCount: 0`.
- Stable runtime baseline currently assumed:
  - object counts stay stable
  - NPC dialogue does not auto-restart
  - objective beacon does not keep respawning
  - console does not spam during the checked flow

## 7. Current open issues
- Startup lag investigation is not finished.
- The main unresolved hotspot inside `HeatwaveRoomNPCSystem.Awake()` is still `InitializeTilePixelsPalette()` at 18.61 ms after the April 5, 2026 optimization.
- Secondary startup phases after that optimization were much smaller:
  - `BuildRoomDecorations` 5.84 ms
  - `RebuildHeatwaveMap` 5.81 ms
- Treat already-fixed TMP rewrite issues as closed unless new evidence appears.

## 8. uloop version note
- Local CLI reports `1.6.3`.
- Global npm package is `uloop-cli@1.6.3`.
- Unity MCP tool responses currently report `Ver: 1.6.4`.
- The project `Packages/` and `ProjectSettings/` do not contain a `1.6.4` version string.
- The project lockfile shows `io.github.hatayama.uloopmcp` coming from a git package source, not a pinned `1.6.4` package entry.
- Treat the prior `1.6.4` mismatch as a tooling warning unless it causes concrete failures.

## 9. Rules for future Codex sessions
- Read this file before editing.
- Do not reopen the fixed-lag theory without new evidence.
- Do not change gameplay behavior while doing performance investigation unless the task explicitly calls for it.
- Prefer measuring startup work in `HeatwaveRoomNPCSystem.Awake()` over speculative UI changes.
- After changes, re-run compile and console checks before claiming success.
- Do not treat the uloop `1.6.4` report as a project bug by itself.

## 10. Next recommended task
If startup work continues, keep using the `HeatwaveStartup` logs and only revisit `InitializeTilePixelsPalette()` if there is new evidence for another safe reduction inside the tile sprite/palette setup path.
