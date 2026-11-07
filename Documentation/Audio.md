# Audio

All sounds and the music loop are **original placeholders synthesised in code**
(`Assets/_Game/Editor/PlaceholderAudioGenerator.cs`). No third-party audio is used.

## Sounds

| Id (`SoundId`) | File (`Assets/_Game/Audio`) | Played when |
|---|---|---|
| UiTap | PH_Sfx_ui_tap.wav | Any button is pressed |
| Place | PH_Sfx_place.wav | A building is placed or moved |
| Remove | PH_Sfx_remove.wav | A building is removed |
| BuildComplete | PH_Sfx_build_complete.wav | Construction finishes, barn expanded |
| Plant | PH_Sfx_plant.wav | A crop is planted, a recipe is queued |
| Harvest | PH_Sfx_harvest.wav | A field is harvested |
| Collect | PH_Sfx_collect.wav | Products are collected from a factory |
| Coins | PH_Sfx_coins.wav | Items sold, order delivered |
| LevelUp | PH_Sfx_level_up.wav | Level up |
| Error | PH_Sfx_error.wav | Barn full, missing ingredients, not enough coins or gems |
| (music) | PH_Music_town_loop.wav | Loops from the first scene onwards (19 s, 100 BPM) |

Volumes: per sound in `ScriptableObjects/SoundLibrary.asset`, and overall via Settings → Music / Sound effects
(`AudioPreferences`, saved per device).

## How it's wired

* `GameAudio` lives on the persistent `GameRoot`, so the music doesn't restart when scenes change.
  It's available as `GameRoot.Current.Context.Audio`.
* `TownAudioBridge` is the only place that maps game events (Planted, Harvested, Collected, Delivered, LeveledUp...)
  to sounds. Change it to re-assign sounds. Gameplay code never plays audio directly.
* `ButtonSounds` (on every Canvas) gives every button, including ones created at runtime, the tap sound.

## Replacing a sound

1. Get or produce an original or properly licensed sound (WAV or OGG, mono is fine).
2. Overwrite the matching `PH_*.wav` file **outside Unity, using the same name**. Unity re-imports it and every reference stays intact.
   Alternatively, assign a new clip to the entry in `SoundLibrary.asset`.
3. Keep effects short (under 1 s) and normalised. Music should loop seamlessly and sit quieter than the effects.
