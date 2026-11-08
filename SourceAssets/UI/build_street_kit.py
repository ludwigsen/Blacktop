"""Render Blacktop's original sprite layers. Run from any directory with Pillow installed."""
from pathlib import Path
from random import Random

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / "Assets/_Project/Art/UI/StreetKit"
RANDOM = Random(7097)
AA = 2


def mask(size):
    """Author at double resolution for crisp diagonals and brush bristles."""
    return Image.new("L", tuple(n * AA for n in size))


def poly(layer, points, value=255):
    ImageDraw.Draw(layer).polygon([(round(x * AA), round(y * AA)) for x, y in points], fill=value)


def line(layer, points, width, value=255):
    ImageDraw.Draw(layer).line(
        [(round(x * AA), round(y * AA)) for x, y in points],
        fill=value, width=max(1, round(width * AA)), joint="curve"
    )


def save(name, alpha, luminance=255):
    """White or neutral grayscale RGB lets Unity supply all theme and team color."""
    if isinstance(luminance, int):
        luminance = Image.new("L", alpha.size, luminance)
    rgba = Image.merge("RGBA", (luminance, luminance, luminance, alpha))
    rgba.resize(tuple(n // AA for n in alpha.size), Image.Resampling.LANCZOS).save(
        DEST / f"{name}.png", optimize=True
    )


def wear(layer, bounds, count, transparent=False):
    """Thin, irregular abrasions kept inside each nine-slice corner cap."""
    x0, y0, x1, y1 = bounds
    for _ in range(count):
        x, y = RANDOM.uniform(x0, x1), RANDOM.uniform(y0, y1)
        length = min(RANDOM.uniform(2, 21), x1 - x)
        line(layer, [(x, y), (x + length, y - length * .18)], RANDOM.uniform(.4, 2.3),
             0 if transparent else RANDOM.randrange(100, 221))


def panel():
    size = (320, 224)
    corners = [(26, 4), (310, 4), (316, 10), (316, 195), (288, 220),
               (10, 220), (4, 214), (4, 29), (26, 4)]
    silhouette = mask(size)
    poly(silhouette, corners)
    gray = Image.new("L", silhouette.size, 253)
    line(gray, corners, 6, 176)
    for bounds in [(5, 4, 54, 46), (265, 178, 314, 221)]:
        wear(gray, bounds, 38)
    # The neutral fill, removable chalk trim and theme tint are independent layers.
    save("AsphaltPanel", silhouette, gray)
    frame = mask(size)
    line(frame, corners, 3, 235)
    for bounds in [(5, 4, 54, 46), (265, 178, 314, 221), (264, 4, 313, 25)]:
        wear(frame, bounds, 31)
    save("ChalkFrame", frame)


def brush():
    size = (768, 192)
    w, h = size
    alpha = mask(size)
    # The center remains straight. Slants, drag and chips stay in the end caps.
    poly(alpha, [(102, 19), (744, 19), (723, 160), (678, 173), (26, 173), (60, 31)])
    gray = Image.new("L", alpha.size, 255)
    for i in range(137):
        y = RANDOM.uniform(14, 181)
        left = 77 - .28 * y
        right = 751 - .29 * y
        x = left + RANDOM.uniform(-26, 37)
        end = right + RANDOM.uniform(-38, 26)
        width = RANDOM.uniform(.6, 3.3)
        line(alpha, [(max(2, x), y), (126, y - .4)], width, RANDOM.randrange(90, 255))
        line(alpha, [(644, y + .4), (min(w - 3, end), y)], width, RANDOM.randrange(90, 255))
        if i % 4 == 0:
            line(gray, [(max(2, x), y), (RANDOM.uniform(60, 121), y)], width, RANDOM.randrange(102, 229))
            line(gray, [(RANDOM.uniform(650, 725), y), (min(w - 3, end), y)], width, RANDOM.randrange(102, 229))
    for y in (25, 29, 153, 159, 165):
        line(gray, [(121, y), (648, y)], 1.25, RANDOM.randrange(160, 222))
    wear(alpha, (12, 12, 122, 178), 33, transparent=True)
    wear(alpha, (649, 12, 756, 178), 33, transparent=True)
    save("BrushPlate", alpha, gray)


def accents():
    stroke = mask((768, 128))
    # A fast rising underline with hundreds of uneven bristle tips.
    for i in range(205):
        y = RANDOM.uniform(21, 102)
        x = max(7, RANDOM.gauss(67, 33))
        end = min(759, RANDOM.gauss(689, 32))
        line(stroke, [(x, y + 16), (235, y + 2), (490, y - 9), (end, y - 15)],
             RANDOM.uniform(.5, 2.9), RANDOM.randrange(95, 256))
    poly(stroke, [(38, 72), (165, 50), (482, 34), (719, 27), (660, 67), (61, 98)])
    wear(stroke, (22, 64, 144, 112), 30, transparent=True)
    wear(stroke, (644, 12, 744, 73), 23, transparent=True)
    save("DryBrush", stroke)

    crown = mask((240, 160))
    outline = [(32, 123), (16, 46), (74, 72), (102, 19), (145, 65), (218, 23), (185, 106)]
    line(crown, outline, 13)
    line(crown, [(34, 120), (182, 104)], 12)
    line(crown, [(43, 138), (185, 118)], 8)
    wear(crown, (22, 36, 220, 136), 23, transparent=True)
    save("PaintCrown", crown)

    spray = mask((384, 256))
    draw = ImageDraw.Draw(spray)
    for _ in range(1000):
        x = RANDOM.gauss(135, 65)
        y = RANDOM.gauss(142 - x * .36, 40)
        radius = min(6, RANDOM.expovariate(1 / 1.5) + .35)
        if 8 < x < 376 and 8 < y < 248:
            draw.ellipse(((x-radius)*AA, (y-radius)*AA, (x+radius)*AA, (y+radius)*AA),
                         fill=RANDOM.randrange(40, 232))
    save("SprayScatter", spray)


def grain_and_scrim():
    grain = mask((192, 192))
    grain.putdata([int(min(255, RANDOM.expovariate(1 / 46))) for _ in range(grain.width * grain.height)])
    save("AsphaltGrain", grain)
    scrim = mask((256, 16))
    ramp = [round(255 * (1 - min(1, max(0, (x / (scrim.width - 1) - .52) / .47)) ** 1.5))
            for x in range(scrim.width)]
    scrim.putdata(ramp * scrim.height)
    save("InkScrim", scrim)


def wordmark():
    font = ImageFont.truetype(str(ROOT / "Assets/_Project/Art/MainMenu/Fonts/BlacktopProDuo.ttf"), 960)
    left, top, right, bottom = font.getbbox("Blacktop")
    glyphs = Image.new("L", (right - left + 32, bottom - top + 32))
    ImageDraw.Draw(glyphs).text((16-left, 16-top), "Blacktop", font=font, fill=255)
    # The existing glyph outlines and spacing are preserved in a fixed brand sprite.
    height = round(1536 * glyphs.height / glyphs.width)
    save("BlacktopWordmark", glyphs.resize((1536 * AA, height * AA), Image.Resampling.LANCZOS))


if __name__ == "__main__":
    DEST.mkdir(parents=True, exist_ok=True)
    panel()
    brush()
    accents()
    grain_and_scrim()
    wordmark()
