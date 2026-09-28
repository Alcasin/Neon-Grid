#!/usr/bin/env python3
"""Deterministically generate Neon Grid's original local prototype audio assets."""

import argparse
import array
import hashlib
import json
import math
import random
import struct
import tempfile
import wave
from pathlib import Path

GENERATOR_VERSION = "neon-grid-procedural-sfx-1.0"
ROOT = Path(__file__).resolve().parents[2]
DEFAULT_MANIFEST = Path(__file__).with_name("audio_manifest.json")
DEFAULT_OUTPUT = ROOT / "Assets" / "NeonGrid" / "Audio"


def clamp(value):
    return max(-1.0, min(1.0, value))


def envelope(t, duration, attack=0.006, release=0.7):
    onset = min(1.0, t / max(attack, 1e-6))
    decay = max(0.0, 1.0 - t / duration) ** release
    return onset * decay


def sine(phase):
    return math.sin(2.0 * math.pi * phase)


def triangle(phase):
    return 2.0 * abs(2.0 * (phase - math.floor(phase + 0.5))) - 1.0


def synthesize(sound):
    rate = sound["sample_rate"]
    duration = sound["duration"]
    count = int(round(rate * duration))
    rng = random.Random(sound["seed"])
    recipe = sound["recipe"]
    if recipe in ("gameplay_ambient_bed", "city_ambient_bed"):
        return synthesize_ambience(sound)
    samples = []
    phase_a = phase_b = phase_c = 0.0
    low_noise = 0.0

    for index in range(count):
        t = index / rate
        x = t / duration
        raw_noise = rng.uniform(-1.0, 1.0)
        low_noise += 0.16 * (raw_noise - low_noise)
        high_noise = raw_noise - low_noise

        if recipe == "rotate_tick":
            freq = 270.0 - 85.0 * x
            phase_a += freq / rate
            body = 0.48 * triangle(phase_a) * envelope(t, duration, 0.002, 2.4)
            transient = 0.28 * high_noise * envelope(t, duration, 0.001, 5.0)
            value = body + transient
        elif recipe == "locked_refusal":
            freq = 155.0 - 38.0 * x
            phase_a += freq / rate
            body = 0.52 * sine(phase_a) * envelope(t, duration, 0.003, 2.8)
            second = 0.18 * sine(phase_a * 0.78) * envelope(
                max(0.0, t - 0.035), max(0.01, duration - 0.035), 0.002, 3.0
            ) if t >= 0.035 else 0.0
            value = body + second + 0.12 * low_noise * envelope(t, duration, 0.001, 4.0)
        elif recipe == "power_rise":
            freq = 165.0 + 410.0 * (x ** 1.4)
            phase_a += freq / rate
            phase_b += (690.0 + 80.0 * x) / rate
            value = envelope(t, duration, 0.008, 1.1) * (
                0.50 * sine(phase_a) + 0.16 * triangle(phase_b) * x +
                0.08 * high_noise * (1.0 - x)
            )
        elif recipe == "power_fall":
            freq = 440.0 - 305.0 * (x ** 0.8)
            phase_a += freq / rate
            value = envelope(t, duration, 0.003, 1.8) * (
                0.50 * sine(phase_a) + 0.12 * low_noise
            )
        elif recipe == "switch_toggle":
            phase_a += (205.0 + 95.0 * x) / rate
            first = triangle(phase_a) * envelope(t, duration, 0.0015, 3.6)
            local = t - 0.044
            second = 0.0
            if local >= 0.0:
                phase_b += 330.0 / rate
                second = sine(phase_b) * envelope(local, duration - 0.044, 0.002, 2.8)
            value = 0.43 * first + 0.31 * second + 0.08 * high_noise * envelope(t, duration, 0.001, 5.0)
        elif recipe == "gate_confirm":
            freq = 265.0 + 120.0 * x
            phase_a += freq / rate
            phase_b += 37.0 / rate
            mod = 0.72 + 0.28 * sine(phase_b)
            value = 0.58 * sine(phase_a) * mod * envelope(t, duration, 0.006, 1.8)
        elif recipe == "objective_warm":
            freq = 228.0 + 62.0 * x
            phase_a += freq / rate
            phase_b += freq * 1.51 / rate
            value = envelope(t, duration, 0.012, 1.25) * (
                0.56 * sine(phase_a) + 0.22 * sine(phase_b)
            )
        elif recipe == "hint_warm_pair":
            first_time = min(t, 0.12)
            second_time = max(0.0, t - 0.095)
            phase_a += 310.0 / rate
            phase_b += 405.0 / rate
            first = sine(phase_a) * envelope(first_time, 0.15, 0.01, 1.8)
            second = sine(phase_b) * envelope(second_time, duration - 0.095, 0.012, 1.5) if t >= 0.095 else 0.0
            value = 0.40 * first + 0.44 * second
        elif recipe == "ui_dry_tap":
            phase_a += 235.0 / rate
            value = envelope(t, duration, 0.001, 4.5) * (
                0.35 * triangle(phase_a) + 0.20 * high_noise
            )
        elif recipe == "completion_resolve":
            events = ((0.0, 246.0, 0.36), (0.19, 337.0, 0.32), (0.39, 421.0, 0.38))
            value = 0.0
            for start, freq, gain in events:
                local = t - start
                if local < 0.0:
                    continue
                phase = local * (freq + 22.0 * min(1.0, local / 0.20))
                value += gain * sine(phase) * envelope(local, duration - start, 0.012, 1.55)
            phase_c += (118.0 + 28.0 * x) / rate
            value += 0.16 * sine(phase_c) * envelope(t, duration, 0.02, 1.4)
        elif recipe == "source_core":
            phase_a += (176.0 + 72.0 * x) / rate
            phase_b += 11.0 / rate
            value = 0.46 * sine(phase_a) * (0.78 + 0.22 * sine(phase_b)) * envelope(t, duration, 0.006, 1.8)
        else:
            raise ValueError(f"Unknown synthesis recipe: {recipe}")
        samples.append(clamp(value))

    peak = max(abs(value) for value in samples) or 1.0
    target = 10.0 ** (sound["normalization_dbfs"] / 20.0)
    gain = min(1.0, target / peak)
    return [clamp(value * gain) for value in samples]


def periodic_frequency(hz, duration):
    """Nearest integer-cycle frequency, exactly periodic over the authored loop."""
    return round(hz * duration) / duration


def synthesize_ambience(sound):
    rate = sound["sample_rate"]
    duration = sound["duration"]
    frame_count = int(round(rate * duration))
    rng = random.Random(sound["seed"])
    recipe = sound["recipe"]
    channels = sound["channels"]
    if channels != 2:
        raise ValueError(f"Ambience must be stereo: {sound['id']}")

    if recipe == "gameplay_ambient_bed":
        body_frequencies = (82.0, 123.0, 167.0)
        body_gains = (0.24, 0.12, 0.07)
        air_range = (190.0, 720.0)
        air_gain = 0.022
        detail_range = (760.0, 1450.0)
        detail_gain = 0.010
        modulation_cycles = (3, 5)
        side_width = 0.075
    elif recipe == "city_ambient_bed":
        body_frequencies = (58.0, 87.0, 132.0)
        body_gains = (0.22, 0.13, 0.065)
        air_range = (120.0, 520.0)
        air_gain = 0.026
        detail_range = (540.0, 1120.0)
        detail_gain = 0.008
        modulation_cycles = (2, 3)
        side_width = 0.12
    else:
        raise ValueError(f"Unknown ambience recipe: {recipe}")

    body = [(periodic_frequency(freq, duration), gain,
             rng.uniform(0.0, 1.0))
            for freq, gain in zip(body_frequencies, body_gains)]
    air = []
    for index in range(7):
        hz = rng.uniform(*air_range)
        air.append((periodic_frequency(hz, duration),
                    air_gain / (1.0 + index * 0.22),
                    rng.uniform(0.0, 1.0), rng.uniform(-0.035, 0.035)))
    details = []
    for index in range(4):
        hz = rng.uniform(*detail_range)
        details.append((periodic_frequency(hz, duration),
                        detail_gain / (1.0 + index * 0.3),
                        rng.uniform(0.0, 1.0), rng.uniform(-0.06, 0.06),
                        4 + index * 2))

    samples = array.array("f")
    peak = 0.0
    for index in range(frame_count):
        x = index / frame_count
        slow_a = 0.76 + 0.24 * sine(modulation_cycles[0] * x + 0.13)
        slow_b = 0.82 + 0.18 * sine(modulation_cycles[1] * x + 0.37)
        center = 0.0
        for frequency, gain, phase in body:
            center += gain * sine(frequency * duration * x + phase) * slow_a
        left_air = right_air = 0.0
        for frequency, gain, phase, offset in air:
            base_phase = frequency * duration * x + phase
            left_air += gain * sine(base_phase + offset)
            right_air += gain * sine(base_phase - offset)
        left_detail = right_detail = 0.0
        for frequency, gain, phase, offset, cycles in details:
            activity = 0.5 + 0.5 * sine(cycles * x + phase * 0.41)
            activity *= activity
            base_phase = frequency * duration * x + phase
            left_detail += gain * activity * sine(base_phase + offset)
            right_detail += gain * activity * sine(base_phase - offset)
        left = center + left_air + left_detail + side_width * (left_air - right_air)
        right = center + right_air + right_detail + side_width * (right_air - left_air)
        samples.append(left)
        samples.append(right)
        peak = max(peak, abs(left), abs(right))

    target = 10.0 ** (sound["normalization_dbfs"] / 20.0)
    gain = min(1.0, target / (peak or 1.0))
    for index in range(len(samples)):
        samples[index] = clamp(samples[index] * gain)
    return samples


def write_wav(path, sound, samples):
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as output:
        output.setnchannels(sound["channels"])
        output.setsampwidth(2)
        output.setframerate(sound["sample_rate"])
        pcm = array.array("h", (int(round(value * 32767.0)) for value in samples))
        if struct.pack("=h", 1) != struct.pack("<h", 1):
            pcm.byteswap()
        output.writeframes(pcm.tobytes())


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(65536), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def load_manifest(path):
    data = json.loads(path.read_text(encoding="utf-8"))
    if data["generator_version"] != GENERATOR_VERSION:
        raise ValueError("Manifest generator_version does not match this generator.")
    if data["origin"] != "procedural_local" or data["external_samples"] is not False:
        raise ValueError("Manifest provenance fields are invalid.")
    return data


def generate(manifest, output_dir, asset_kind=None):
    hashes = {}
    for sound in manifest["sounds"]:
        kind = sound.get("asset_kind", "sfx")
        if asset_kind is not None and kind != asset_kind:
            continue
        if sound["channels"] not in (1, 2) or sound["sample_rate"] != 48000:
            raise ValueError(f"Unsupported format for {sound['id']}")
        if kind == "ambience":
            destination = output_dir / "Ambience" / sound["filename"]
        else:
            destination = output_dir / "SFX" / sound["category"] / sound["filename"]
        write_wav(destination, sound, synthesize(sound))
        hashes[sound["id"]] = sha256(destination)
    return hashes


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--write-hashes", action="store_true")
    parser.add_argument("--verify", action="store_true")
    parser.add_argument("--kind", choices=("sfx", "ambience"))
    args = parser.parse_args()
    manifest = load_manifest(args.manifest)

    if args.verify:
        with tempfile.TemporaryDirectory(prefix="neon_grid_sfx_") as temporary:
            generated = generate(manifest, Path(temporary), args.kind)
        expected = {sound["id"]: sound.get("sha256", "") for sound in manifest["sounds"]}
        mismatches = [sound_id for sound_id, value in generated.items()
                      if value != expected.get(sound_id)]
        if mismatches:
            raise SystemExit("Hash mismatch: " + ", ".join(mismatches))
        print(f"Verified {len(generated)} deterministic procedural audio files.")
        return

    generated = generate(manifest, args.output_dir, args.kind)
    if args.write_hashes:
        for sound in manifest["sounds"]:
            if sound["id"] in generated:
                sound["sha256"] = generated[sound["id"]]
        args.manifest.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    for sound_id, digest in generated.items():
        print(f"{sound_id}: {digest}")


if __name__ == "__main__":
    main()
