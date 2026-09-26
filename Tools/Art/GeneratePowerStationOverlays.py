"""Deterministically author registered Power Station overlay PNGs from the locked Base.

This script never edits, crops, scales, or re-encodes PowerStation_Base.png. It draws a
small set of hand-authored light masks in the Base's exact 1254 x 1254 coordinate space.
"""

from hashlib import sha256
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont


ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "Assets/NeonGrid/Art/CityBuildings/PowerStation"
BASE_PATH = ART / "PowerStation_Base.png"
EXPECTED_BASE_SHA256 = "c713b39d3e579a13ddc3f4672896d7f49e32d203c918fdca5d7f7698e05cc343"
PREVIEW_PATH = ROOT / "Assets/NeonGrid/Documentation/Previews/PowerStation_StatePreview.png"
CANVAS = (1254, 1254)


def assert_base_contract() -> Image.Image:
    payload = BASE_PATH.read_bytes()
    assert sha256(payload).hexdigest() == EXPECTED_BASE_SHA256, "Locked Base hash changed."
    base = Image.open(BASE_PATH)
    assert base.size == CANVAS and base.mode == "RGBA"
    assert base.getchannel("A").getextrema() == (0, 255)
    return base.copy()


def mask() -> Image.Image:
    return Image.new("L", CANVAS, 0)


def registered_effect(base_alpha: Image.Image, marks: Image.Image, color: tuple[int, int, int],
                      glow_radius: float, glow_alpha: int, core_alpha: int = 255) -> Image.Image:
    # Solid marks can only exist on authored building pixels. A compact dilation permits
    # local halo beyond the edge without introducing a rectangular canvas artifact.
    solid = ImageChops.multiply(marks, base_alpha)
    vicinity = base_alpha.filter(ImageFilter.MaxFilter(31))
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
    # Three compact service-light clusters remain on existing facade/machinery panels.
    # Their segmented geometry is deliberately large enough to survive the 189 x 105
    # map footprint without tracing rails or outlining the architecture.
    for top, bottom in [(858, 871), (878, 891), (898, 911)]:
        draw.polygon([(546, top + 5), (565, top), (565, bottom), (546, bottom + 6)],
                     fill=238)
    for top, bottom in [(881, 894), (901, 914), (921, 934)]:
        draw.polygon([(708, top + 6), (728, top), (728, bottom), (708, bottom + 6)],
                     fill=246)
    for top, bottom in [(794, 805), (811, 822), (828, 839)]:
        draw.polygon([(980, top + 5), (999, top), (999, bottom), (980, bottom + 6)],
                     fill=226)
    # A few subordinate machinery indicators support the clusters without becoming
    # an architectural outline or competing with later cyan restoration energy.
    for x, y in [(1014, 767), (185, 782), (265, 807), (580, 686), (700, 729)]:
        draw.ellipse((x - 6, y - 5, x + 6, y + 5), fill=214)
    result = registered_effect(base.getchannel("A"), amber, (255, 174, 48), 10, 108)

    white = mask()
    white_draw = ImageDraw.Draw(white)
    for x, y in [(555, 865), (718, 888), (990, 801), (585, 686)]:
        white_draw.ellipse((x - 3, y - 3, x + 3, y + 3), fill=240)
    return Image.alpha_composite(result,
        registered_effect(base.getchannel("A"), white, (255, 241, 190), 4, 72, 235))


def energy_overlay(base: Image.Image) -> Image.Image:
    cyan = mask()
    draw = ImageDraw.Draw(cyan)
    # Selected major pipe/conduit runs only. These follow existing authored geometry.
    routes = [
        [(355, 676), (389, 673), (423, 688), (460, 704), (495, 704)],
        [(841, 609), (875, 606), (914, 623), (951, 650), (980, 678), (1015, 696)],
        [(669, 620), (704, 620), (738, 635), (769, 651)],
        [(327, 962), (366, 971), (411, 991), (460, 1003), (505, 1014)],
    ]
    for route in routes:
        draw.line(route, fill=225, width=8, joint="curve")
    # Restricted stack-base infrastructure arcs.
    draw.arc((353, 548, 475, 624), 18, 161, fill=190, width=7)
    draw.arc((485, 585, 608, 657), 18, 161, fill=190, width=7)
    return registered_effect(base.getchannel("A"), cyan, (30, 220, 232), 12, 105, 245)


def core_overlay(base: Image.Image) -> Image.Image:
    nodes = mask()
    draw = ImageDraw.Draw(nodes)
    # Final-state junctions only; Power Station has no large reactor/core shape.
    for x, y, radius in [
        (497, 704, 7), (839, 611, 7), (770, 651, 6),
        (657, 690, 7), (356, 676, 6), (505, 1014, 6),
    ]:
        draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=245)
    result = registered_effect(base.getchannel("A"), nodes, (92, 244, 250), 16, 135)

    hot = mask()
    hot_draw = ImageDraw.Draw(hot)
    for x, y in [(497, 704), (839, 611), (657, 690)]:
        hot_draw.ellipse((x - 3, y - 3, x + 3, y + 3), fill=255)
    return Image.alpha_composite(result,
        registered_effect(base.getchannel("A"), hot, (224, 255, 255), 5, 100, 255))


def save_overlay(image: Image.Image, name: str) -> Path:
    assert image.size == CANVAS and image.mode == "RGBA"
    path = ART / name
    image.save(path, format="PNG", optimize=True)
    return path


def state_preview(base: Image.Image, warm: Image.Image, energy: Image.Image,
                  core: Image.Image) -> None:
    profiles = [
        ("LOCKED", (1.0, 0.0, 0.0, 0.0)),
        ("STAGE 1", (1.0, 0.42, 0.0, 0.0)),
        ("STAGE 2", (1.0, 0.72, 0.24, 0.0)),
        ("STAGE 3", (1.0, 0.88, 0.63, 0.12)),
        ("RESTORED", (1.0, 1.0, 1.0, 0.65)),
    ]
    panel_size = (213, 153)
    preview = Image.new("RGB", (panel_size[0] * len(profiles), panel_size[1]), (5, 9, 16))
    font = ImageFont.load_default()
    for index, (label, alpha) in enumerate(profiles):
        composite = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
        for layer, opacity in zip((base, warm, energy, core), alpha):
            current = layer.copy()
            if opacity < 1:
                current.putalpha(current.getchannel("A").point(lambda value: round(value * opacity)))
            composite = Image.alpha_composite(composite, current)
        mapped = composite.resize((189, 105), Image.Resampling.LANCZOS)
        panel = Image.new("RGBA", panel_size, (7, 14, 25, 255))
        panel.alpha_composite(mapped, (12, 28))
        ImageDraw.Draw(panel).text((12, 8), label, fill=(225, 240, 247, 255), font=font)
        preview.paste(panel.convert("RGB"), (index * panel_size[0], 0))
    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    preview.save(PREVIEW_PATH, format="PNG", optimize=True)


def main() -> None:
    base = assert_base_contract()
    warm = warm_overlay(base)
    energy = energy_overlay(base)
    core = core_overlay(base)
    paths = [
        save_overlay(warm, "PowerStation_WarmLights.png"),
        save_overlay(energy, "PowerStation_Energy.png"),
        save_overlay(core, "PowerStation_Core.png"),
    ]
    state_preview(base, warm, energy, core)
    assert sha256(BASE_PATH.read_bytes()).hexdigest() == EXPECTED_BASE_SHA256
    for path in paths:
        image = Image.open(path)
        assert image.size == CANVAS and image.mode == "RGBA"
        assert image.getchannel("A").getextrema()[0] == 0
        print(path.relative_to(ROOT), image.getbbox(), path.stat().st_size)
    print(PREVIEW_PATH.relative_to(ROOT), PREVIEW_PATH.stat().st_size)


if __name__ == "__main__":
    main()
