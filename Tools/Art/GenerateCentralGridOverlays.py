"""Deterministically author registered Central Grid overlays from the locked Base.

The authoritative Base is never rewritten. Hand-authored masks add only controlled
lighting in the Base's exact 1278 x 1278 coordinate system.
"""

from hashlib import sha256
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont


ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "Assets/NeonGrid/Art/CityBuildings/CentralGrid"
POWER_ART = ROOT / "Assets/NeonGrid/Art/CityBuildings/PowerStation"
BASE_PATH = ART / "CentralGrid_Base.png"
EXPECTED_BASE_SHA256 = "c42fa4773c5a0430d7e6df2a559b895f065b66f6181af52b51baaae1858b3ecb"
PREVIEW_DIRECTORY = ROOT / "Assets/NeonGrid/Documentation/Previews"
STATE_PREVIEW_PATH = PREVIEW_DIRECTORY / "CentralGrid_StatePreview.png"
COMPARISON_PREVIEW_PATH = PREVIEW_DIRECTORY / "PowerStation_CentralGrid_Comparison.png"
CANVAS = (1278, 1278)


def assert_base_contract() -> Image.Image:
    payload = BASE_PATH.read_bytes()
    assert sha256(payload).hexdigest() == EXPECTED_BASE_SHA256, "Locked Base hash changed."
    base = Image.open(BASE_PATH)
    assert base.size == CANVAS and base.mode == "RGBA"
    assert base.getchannel("A").getextrema() == (0, 255)
    return base.copy()


def mask() -> Image.Image:
    return Image.new("L", CANVAS, 0)


def registered_effect(base_alpha: Image.Image, marks: Image.Image,
                      color: tuple[int, int, int], glow_radius: float,
                      glow_alpha: int, core_alpha: int = 255) -> Image.Image:
    solid = ImageChops.multiply(marks, base_alpha)
    vicinity = base_alpha.filter(ImageFilter.MaxFilter(41))
    glow = ImageChops.multiply(marks.filter(ImageFilter.GaussianBlur(glow_radius)), vicinity)
    glow = glow.point(lambda value: value * glow_alpha // 255)
    solid = solid.point(lambda value: value * core_alpha // 255)
    result = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    glow_layer = Image.new("RGBA", CANVAS, (*color, 0))
    glow_layer.putalpha(glow)
    result = Image.alpha_composite(result, glow_layer)
    core_layer = Image.new("RGBA", CANVAS, (*color, 0))
    core_layer.putalpha(solid)
    return Image.alpha_composite(result, core_layer)


def warm_overlay(base: Image.Image) -> Image.Image:
    amber = mask()
    draw = ImageDraw.Draw(amber)

    # Four readable service-light banks on the major distribution sectors.
    banks = [
        [(258, 591), (339, 608)],
        [(931, 607), (1012, 589)],
        [(231, 911), (328, 864)],
        [(950, 866), (1046, 912)],
    ]
    for start, end in banks:
        draw.line((start, end), fill=238, width=13)

    # Restrained inner-ring activity and the forward service module.
    draw.arc((468, 558, 812, 778), 24, 66, fill=228, width=11)
    draw.arc((468, 558, 812, 778), 114, 156, fill=228, width=11)
    draw.arc((468, 558, 812, 778), 204, 246, fill=228, width=11)
    draw.arc((468, 558, 812, 778), 294, 336, fill=228, width=11)
    for x, y in [(604, 1127), (624, 1130), (644, 1131), (664, 1128)]:
        draw.rounded_rectangle((x - 7, y - 4, x + 7, y + 4), 3, fill=230)

    # Tower/service indicators remain warm and do not fill the core shaft.
    for x, y in [(560, 453), (708, 477), (587, 609), (690, 608),
                 (385, 650), (892, 650)]:
        draw.rounded_rectangle((x - 7, y - 13, x + 7, y + 13), 4, fill=236)
    result = registered_effect(base.getchannel("A"), amber, (255, 163, 38), 11, 112)

    hot = mask()
    hot_draw = ImageDraw.Draw(hot)
    for x, y in [(298, 600), (972, 599), (282, 887), (997, 889),
                 (560, 451), (708, 475), (634, 1129)]:
        hot_draw.ellipse((x - 4, y - 4, x + 4, y + 4), fill=245)
    return Image.alpha_composite(result,
        registered_effect(base.getchannel("A"), hot, (255, 238, 178), 5, 85, 245))


def energy_overlay(base: Image.Image) -> Image.Image:
    cyan = mask()
    draw = ImageDraw.Draw(cyan)

    # Selected ring sectors and five primary distribution routes. They emphasize the
    # radial hub without tracing every edge or replacing graphite architecture.
    ring = (402, 522, 876, 882)
    for start in (12, 102, 192, 282):
        draw.arc(ring, start, start + 58, fill=225, width=10)
    routes = [
        [(493, 685), (423, 661), (354, 631), (289, 602)],
        [(785, 684), (855, 658), (926, 628), (992, 600)],
        [(571, 793), (495, 831), (414, 869), (332, 904)],
        [(707, 793), (786, 830), (866, 869), (948, 905)],
        [(640, 808), (640, 888), (640, 972), (640, 1060)],
    ]
    for route in routes:
        draw.line(route, fill=230, width=9, joint="curve")

    # Paired tower conduits stop below the beacon tips and leave the shaft readable.
    draw.line([(620, 596), (619, 474), (619, 353), (625, 229)], fill=235, width=8)
    draw.line([(661, 592), (661, 469), (660, 347), (657, 214)], fill=235, width=8)
    for x, y in [(493, 685), (785, 684), (571, 793), (707, 793), (640, 808)]:
        draw.ellipse((x - 7, y - 7, x + 7, y + 7), fill=242)
    return registered_effect(base.getchannel("A"), cyan, (25, 218, 232), 14, 118, 248)


def core_overlay(base: Image.Image) -> Image.Image:
    core = mask()
    draw = ImageDraw.Draw(core)

    # The final-state focal column is narrow and registered to existing tower faces.
    draw.line([(640, 567), (640, 448), (640, 324), (644, 202), (650, 91)],
              fill=248, width=13)
    draw.arc((510, 574, 768, 750), 8, 172, fill=235, width=11)
    draw.arc((510, 574, 768, 750), 188, 352, fill=235, width=11)
    for x, y in [(493, 685), (785, 684), (571, 793), (707, 793), (640, 808)]:
        draw.ellipse((x - 9, y - 9, x + 9, y + 9), fill=235)
    result = registered_effect(base.getchannel("A"), core, (74, 235, 246), 19, 145)

    white = mask()
    white_draw = ImageDraw.Draw(white)
    white_draw.line([(642, 515), (642, 385), (643, 255), (649, 113)], fill=255, width=6)
    for x, y in [(640, 637), (493, 685), (785, 684)]:
        white_draw.ellipse((x - 4, y - 4, x + 4, y + 4), fill=255)
    return Image.alpha_composite(result,
        registered_effect(base.getchannel("A"), white, (224, 255, 255), 8, 125, 255))


def save_overlay(image: Image.Image, name: str) -> Path:
    assert image.size == CANVAS and image.mode == "RGBA"
    path = ART / name
    image.save(path, format="PNG", optimize=True)
    return path


PROFILES = [
    ("LOCKED", (1.0, 0.0, 0.0, 0.0)),
    ("STAGE 1", (1.0, 0.32, 0.0, 0.0)),
    ("STAGE 2", (1.0, 0.70, 0.22, 0.0)),
    ("STAGE 3", (1.0, 0.90, 0.72, 0.30)),
    ("RESTORED", (1.0, 1.0, 1.0, 1.0)),
]


def composite_state(layers: tuple[Image.Image, ...], alpha: tuple[float, ...]) -> Image.Image:
    composite = Image.new("RGBA", layers[0].size, (0, 0, 0, 0))
    for layer, opacity in zip(layers, alpha):
        current = layer.copy()
        if opacity < 1:
            current.putalpha(current.getchannel("A").point(lambda value: round(value * opacity)))
        composite = Image.alpha_composite(composite, current)
    return composite


def state_preview(layers: tuple[Image.Image, ...]) -> None:
    panel_size = (270, 202)
    mapped_size = (246, 154)
    preview = Image.new("RGB", (panel_size[0] * len(PROFILES), panel_size[1]), (5, 9, 16))
    font = ImageFont.load_default()
    for index, (label, alpha) in enumerate(PROFILES):
        mapped = composite_state(layers, alpha).resize(mapped_size, Image.Resampling.LANCZOS)
        panel = Image.new("RGBA", panel_size, (7, 14, 25, 255))
        panel.alpha_composite(mapped, (12, 34))
        ImageDraw.Draw(panel).text((12, 10), label, fill=(225, 240, 247, 255), font=font)
        preview.paste(panel.convert("RGB"), (index * panel_size[0], 0))
    PREVIEW_DIRECTORY.mkdir(parents=True, exist_ok=True)
    preview.save(STATE_PREVIEW_PATH, format="PNG", optimize=True)


def comparison_preview(central_layers: tuple[Image.Image, ...]) -> None:
    power_layers = tuple(Image.open(POWER_ART / name).convert("RGBA") for name in (
        "PowerStation_Base.png", "PowerStation_WarmLights.png",
        "PowerStation_Energy.png", "PowerStation_Core.png"))
    power = composite_state(power_layers, (1.0, 1.0, 1.0, 0.65)).resize(
        (189, 105), Image.Resampling.LANCZOS)
    central = composite_state(central_layers, PROFILES[-1][1]).resize(
        (246, 154), Image.Resampling.LANCZOS)
    preview = Image.new("RGBA", (520, 220), (7, 14, 25, 255))
    preview.alpha_composite(power, (28, 82))
    preview.alpha_composite(central, (252, 45))
    draw = ImageDraw.Draw(preview)
    font = ImageFont.load_default()
    draw.text((28, 26), "FINAL POWER STATION — 189 x 105", fill=(225, 240, 247), font=font)
    draw.text((252, 26), "FINAL CENTRAL GRID — 246 x 154", fill=(225, 240, 247), font=font)
    preview.convert("RGB").save(COMPARISON_PREVIEW_PATH, format="PNG", optimize=True)


def main() -> None:
    base = assert_base_contract()
    layers = (base, warm_overlay(base), energy_overlay(base), core_overlay(base))
    paths = [
        save_overlay(layers[1], "CentralGrid_WarmLights.png"),
        save_overlay(layers[2], "CentralGrid_Energy.png"),
        save_overlay(layers[3], "CentralGrid_Core.png"),
    ]
    state_preview(layers)
    comparison_preview(layers)
    assert sha256(BASE_PATH.read_bytes()).hexdigest() == EXPECTED_BASE_SHA256
    for path in paths:
        image = Image.open(path)
        assert image.size == CANVAS and image.mode == "RGBA"
        assert image.getchannel("A").getextrema()[0] == 0
        print(path.relative_to(ROOT), image.getbbox(), path.stat().st_size)
    print(STATE_PREVIEW_PATH.relative_to(ROOT), STATE_PREVIEW_PATH.stat().st_size)
    print(COMPARISON_PREVIEW_PATH.relative_to(ROOT), COMPARISON_PREVIEW_PATH.stat().st_size)


if __name__ == "__main__":
    main()
