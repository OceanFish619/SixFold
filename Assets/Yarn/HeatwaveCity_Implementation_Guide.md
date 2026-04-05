# Heatwave City - Yarn Spinner Integration Guide

This package contains first-city narrative content in English for Unity + Yarn Spinner.

## Files
- `HeatwaveCity_SimpleEnglish.yarn`
- `HeatwaveCity_Main.yarn` (expanded version)
- `HeatwaveCity_UI_Text.csv`
- `Assets/Scripts/HeatwaveCityGameController.cs`
- `Assets/Scripts/HeatwaveDialoguePresenter.cs`

## Recommended Starting Node
Use `C1_START`.

## Core Variables
- `$heat_safety`
- `$community_trust`
- `$infrastructure_stability`
- `$city_health`
- `$blackout_risk`
- `$cooling_centers_opened`

## Unity Setup (Quick)
1. Create a `Yarn Project` asset (`Assets -> Create -> Yarn Spinner -> Yarn Project`).
2. Add `HeatwaveCity_SimpleEnglish.yarn` as a source in that Yarn Project.
3. Press Play. `HeatwaveCityGameController` auto-bootstraps runtime systems.
4. Press `T` in play mode to start dialogue at node `C1_START`.

## What Runtime Now Does Automatically
- Applies the two-box dialogue layout (left speaker, right content/options).
- Creates/fetches a `DialogueRunner`.
- Adds `HeatwaveDialoguePresenter` to `DialoguePanel`.
- Builds a top-left Heatwave status HUD.
- Shows current Yarn variable values while dialogue runs.

## Gameplay Mapping Suggestion
- HUD bars map directly to the core variables.
- On each choice, update HUD immediately to show tradeoffs.
- Trigger ending panel when dialogue reaches:
  - `C1_ENDING_CRISIS`
  - `C1_ENDING_SURVIVAL`
  - `C1_ENDING_RESILIENT`

## Writing Style Note
Dialogue is intentionally in simple English for easy expansion, localization, and voice pass.
