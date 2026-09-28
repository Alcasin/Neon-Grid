# M16-B1 audio provenance

All M16-B1 sound-effect source files were generated locally and procedurally by
original project code. No external samples, source recordings, downloaded audio,
online generators, impulse responses, synth presets, or third-party audio packages
were used.

- Generator: `Tools/AudioGeneration/generate_audio.py`
- Manifest: `Tools/AudioGeneration/audio_manifest.json`
- Generator version: `neon-grid-procedural-sfx-1.0`
- Output: `Assets/NeonGrid/Audio/SFX/`
- Source format: mono, 48 kHz, signed 16-bit PCM WAV
- Origin: `procedural_local`
- External samples: `false`

The generator uses Python's standard library only. It is an explicit development
tool and is not called by Unity startup, import, build, Play Mode, or runtime code.
Runtime code loads the committed WAV files as ordinary Unity `AudioClip` assets.

## Deterministic seeds and source hashes

| Sound ID | Seed | SHA-256 |
|---|---:|---|
| tile_rotate | 1601 | `4699F1FC5D4AAC538ACDE4E21DCA01A3A5AC70D5BC876D4BBA9679D323146FE6` |
| locked_reject | 1602 | `BDBDDC4E1D2C9B917EA06958B372770FE0E414EC1CBAB0A7AB3FB712C907FC28` |
| power_activate | 1603 | `14E4B9D97FDE426FC85B07C6BCCB98AC583146A94D08C434E447093960A65ED7` |
| power_deactivate | 1604 | `1DDB7565FF0C1CBFD2E894D27EAA24C16B941AA5E6A9DD31E0906892B5D3863E` |
| switch_toggle | 1605 | `4A957B16C15344C5B2B666A34834A2FA8D4EB5E3CD65D8C8E4717F45B7E49E4C` |
| gate_activate | 1606 | `2208C6DA36892FD4FD2DD261972C0D7481A1A7076D9DF1ACCFCDCB056C51E91A` |
| objective_activate | 1607 | `2EC093080A55E12C4E94763C362941EDC567FEA9E597B1C535A0FE5040F81090` |
| hint | 1608 | `A788A7CC973F784428436445A0D91C53FE1FA1BC446EE4D1077EC76B0092325F` |
| ui_button | 1609 | `1D85F88719EF633BE7EDFBD92584740A4123C60A341F6A78127040BB8F5B4CEA` |
| completion | 1610 | `ACEC17CFE0BEDD1D13E3EE26F8196D542916ED2C13868AE56BEF5D5A7E8D8DEB` |
| source_pulse | 1611 | `B0B7EBFBDE7956B974233D5FCDF5FCF4DFB9D718DEAD7D22AC9DF332D1941E7C` |

The manifest is the authoritative record of recipe names, durations,
normalization targets, parameters, seeds, and hashes.

## Regeneration and verification

From the repository root, explicit regeneration plus hash-manifest update is:

```text
python Tools/AudioGeneration/generate_audio.py --write-hashes
```

Reproduce into a temporary directory and verify byte-for-byte determinism without
changing committed assets:

```text
python Tools/AudioGeneration/generate_audio.py --verify
```

Generation is never automatic. Updating hashes is intentionally opt-in so an
accidental waveform change is detected rather than silently accepted.

## Unity import policy

Only WAV files below `Assets/NeonGrid/Audio/SFX/` receive the local import policy:
force mono, preserve the authored 48 kHz sample rate, preload, PCM, and
`Decompress On Load`. These assets are short one-shots; the policy avoids decode
latency and compression artifacts while keeping the total resident size small.
No global Unity audio setting is modified.

## M16-B2 procedural ambience

The B2 prototype adds two locally generated ambient beds. They use the same
dependency-free generator and contain no downloads, recordings, samples, presets,
or external source audio.

| ID | Seed | Duration | Channels | SHA-256 |
|---|---:|---:|---:|---|
| gameplay_ambience | 3201 | 28.0 s | 2 | `491A2BBEDE962B2069C7EA03FA8D81E4D177DB13623A511FF878041719AC39ED` |
| city_ambience | 3202 | 30.0 s | 2 | `C9E309D8922F839C551A2D07110C54032F26A6B9C86CB93F2335D809B60951E0` |

Gameplay ambience combines a restrained 82/123/167 Hz infrastructure body,
slow periodic modulation, softly shaped synthetic air, and very low deterministic
micro-detail. City ambience uses a broader 58/87/132 Hz body, slower modulation,
softer upper detail, and slightly greater—but still center-coherent—stereo width.
Neither recipe contains melody, chord progression, beat, field recording, or
one-off landmark event.

Every oscillator and modulation rate completes an integer number of cycles over
the loop. Seeded spectral components also use integer-cycle frequencies, so the
waveform and its modulation wrap continuously without a fade-to-silence gap.
Source files are stereo 48 kHz, signed 16-bit PCM WAV. Conservative peaks of
-15 dBFS (Gameplay) and -16 dBFS (City), followed by runtime ambience gains of
0.153 for Gameplay and 0.18 for City, keep SFX in the foreground. The Gameplay
default reflects the accepted M16-B2 listening pass; City remains unchanged.

Generate only B2 ambience, without rewriting B1 SFX:

```text
python Tools/AudioGeneration/generate_audio.py --kind ambience --write-hashes
```

Verify deterministic ambience reproduction in a temporary directory:

```text
python Tools/AudioGeneration/generate_audio.py --verify --kind ambience
```

Unity imports these longer loops as Vorbis quality 0.55, Compressed In Memory,
preloaded, stereo, and at the authored 48 kHz sample rate. This avoids the roughly
5.4–5.8 MB decoded resident cost per PCM loop while keeping these short 28–30
second assets responsive. The two dedicated looping ambience voices are separate
from the six SFX voices and crossfade over 0.8 seconds. Repeated requests for the
current mode do not restart or stack playback. Ambience remains prototype-only;
no campaign or save binding is present.

M17 Settings retains the requirement for separate user-adjustable SFX and
Ambience volume controls, with a likely Master volume parent. M16-B2 provides
only the accepted defaults and does not add settings UI or persistence.

The prototype buttons `AMB: GAMEPLAY`, `AMB: CITY`, and `AMB: NONE` provide an
explicit audition path. Listen to each active mode for at least five minutes on a
phone speaker and headphones, exercise all accepted B1 SFX over it, and check for
fatigue, obvious repetition, boundary clicks, masking, and mono compatibility.

## Prototype listening QA

`HOLD COMPLETION` is a presentation QA control only. It holds back the result
panel; it does not undo authoritative completion or make a completed board
interactable. PS01 solves on its first valid rotation, so it is suitable for
checking rotate, power, objective, and completion hierarchy but not long repeated
input.

Use CG10 for extended audio lifetime checks: rotate at least 20 times, exercise
locked rejection, switch, gate, power on/off, and an actual hint, then restart and
repeat. Further taps on an already completed PS01 are not expected to emit tile
audio because `GameplaySession.CanInteract` is false.

This document records factual project provenance and does not make legal
guarantees.
