from pathlib import Path
import struct
import zlib

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "ServerScheduler" / "Assets"
OUT.mkdir(parents=True, exist_ok=True)
SCALE = 4


def blend(px, w, h, x, y, color):
    if x < 0 or y < 0 or x >= w or y >= h:
        return
    i = (y * w + x) * 4
    sr, sg, sb, sa = color
    a = sa / 255.0
    if a <= 0:
        return
    px[i] = int(sr * a + px[i] * (1 - a))
    px[i + 1] = int(sg * a + px[i + 1] * (1 - a))
    px[i + 2] = int(sb * a + px[i + 2] * (1 - a))
    px[i + 3] = int(255 * a + px[i + 3] * (1 - a))


def rounded_gradient(px, w, h, box, radius, top, bottom, alpha=255):
    x0, y0, x1, y1 = [int(round(v)) for v in box]
    r = max(0, int(round(radius)))
    for y in range(max(0, y0), min(h, y1)):
        t = (y - y0) / max(1, y1 - y0 - 1)
        color = [int(top[i] * (1 - t) + bottom[i] * t) for i in range(3)] + [alpha]
        for x in range(max(0, x0), min(w, x1)):
            cx = min(max(x, x0 + r), x1 - 1 - r)
            cy = min(max(y, y0 + r), y1 - 1 - r)
            if (x - cx) ** 2 + (y - cy) ** 2 <= r ** 2:
                blend(px, w, h, x, y, color)


def rounded_rect(px, w, h, box, radius, color):
    x0, y0, x1, y1 = [int(round(v)) for v in box]
    r = max(0, int(round(radius)))
    for y in range(max(0, y0), min(h, y1)):
        for x in range(max(0, x0), min(w, x1)):
            cx = min(max(x, x0 + r), x1 - 1 - r)
            cy = min(max(y, y0 + r), y1 - 1 - r)
            if (x - cx) ** 2 + (y - cy) ** 2 <= r ** 2:
                blend(px, w, h, x, y, color)


def stroke_rounded(px, w, h, box, radius, thickness, color, inner_top, inner_bottom):
    rounded_rect(px, w, h, box, radius, color)
    t = int(round(thickness))
    inner = (box[0] + t, box[1] + t, box[2] - t, box[3] - t)
    rounded_gradient(px, w, h, inner, max(0, radius - t), inner_top, inner_bottom)


def rect(px, w, h, box, color):
    x0, y0, x1, y1 = [int(round(v)) for v in box]
    for y in range(max(0, y0), min(h, y1)):
        for x in range(max(0, x0), min(w, x1)):
            blend(px, w, h, x, y, color)


def downsample(px, w, h, scale):
    ow, oh = w // scale, h // scale
    out = bytearray(ow * oh * 4)
    samples = scale * scale
    for y in range(oh):
        for x in range(ow):
            sums = [0, 0, 0, 0]
            for sy in range(scale):
                for sx in range(scale):
                    i = ((y * scale + sy) * w + x * scale + sx) * 4
                    for c in range(4):
                        sums[c] += px[i + c]
            i = (y * ow + x) * 4
            out[i:i + 4] = [v // samples for v in sums]
    return out


def png_bytes(width, height, pixels):
    raw = b"".join(b"\x00" + bytes(pixels[y * width * 4:(y + 1) * width * 4]) for y in range(height))
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xffffffff)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")


def make_icon(width, height, path):
    w, h = width * SCALE, height * SCALE
    px = bytearray(w * h * 4)
    s = min(w, h)
    cx, cy = w / 2, h / 2

    # Soft shadow layers: neutral, no accent-colored frame.
    for offset, radius, alpha in ((0.038, 0.24, 18), (0.026, 0.22, 24), (0.014, 0.20, 34)):
        size = s * (0.84 - offset * 0.5)
        x0, y0 = cx - size / 2, cy - size / 2 + s * offset
        rounded_rect(px, w, h, (x0, y0, x0 + size, y0 + size), size * radius, (48, 72, 104, alpha))

    # Glass tile with a cool, almost-neutral gradient.
    size = s * 0.84
    x0, y0 = cx - size / 2, cy - size / 2
    x1, y1 = x0 + size, y0 + size
    radius = size * 0.22
    top, bottom = (250, 252, 255), (222, 234, 249)
    rounded_rect(px, w, h, (x0, y0, x1, y1), radius, (148, 181, 218, 210))
    rounded_gradient(px, w, h, (x0 + s * 0.006, y0 + s * 0.006, x1 - s * 0.006, y1 - s * 0.006), radius * 0.95, top, bottom)

    # Thin top highlight and soft border.
    rect(px, w, h, (x0 + s * 0.09, y0 + s * 0.025, x1 - s * 0.09, y0 + s * 0.031), (255, 255, 255, 150))
    stroke_rounded(px, w, h, (x0 + s * 0.012, y0 + s * 0.012, x1 - s * 0.012, y1 - s * 0.012), radius * 0.88, s * 0.006, (160, 190, 224, 145), top, bottom)

    # Minimal calendar line art.
    ink = (29, 78, 139, 245)
    faint = (126, 164, 211, 210)
    accent = (37, 99, 235, 255)
    cal_x0, cal_y0 = cx - s * 0.255, cy - s * 0.215
    cal_x1, cal_y1 = cx + s * 0.255, cy + s * 0.255
    stroke_rounded(px, w, h, (cal_x0, cal_y0, cal_x1, cal_y1), s * 0.045, s * 0.013, ink, top, bottom)
    rect(px, w, h, (cal_x0 + s * 0.014, cal_y0 + s * 0.125, cal_x1 - s * 0.014, cal_y0 + s * 0.138), ink)
    for bx in (cx - s * 0.145, cx + s * 0.115):
        rounded_rect(px, w, h, (bx, cal_y0 - s * 0.055, bx + s * 0.028, cal_y0 + s * 0.075), s * 0.014, ink)

    for gx in (cx - s * 0.085, cx + s * 0.085):
        rect(px, w, h, (gx, cal_y0 + s * 0.19, gx + s * 0.006, cal_y1 - s * 0.055), faint)
    for gy in (cal_y0 + s * 0.255, cal_y0 + s * 0.335):
        rect(px, w, h, (cal_x0 + s * 0.055, gy, cal_x1 - s * 0.055, gy + s * 0.006), faint)

    # One selected day: restrained single accent.
    rounded_rect(px, w, h, (cx - s * 0.075, cal_y0 + s * 0.205, cx + s * 0.005, cal_y0 + s * 0.285), s * 0.012, accent)

    small = downsample(px, w, h, SCALE)
    path.write_bytes(png_bytes(width, height, small))


sizes = {
    "Square44x44Logo.png": (44, 44),
    "Square71x71Logo.png": (71, 71),
    "Square150x150Logo.png": (150, 150),
    "Square310x310Logo.png": (310, 310),
    "Wide310x150Logo.png": (310, 150),
    "StoreLogo.png": (50, 50),
    "SplashScreen.png": (620, 300),
}
for name, (width, height) in sizes.items():
    make_icon(width, height, OUT / name)

make_icon(256, 256, OUT / "AppIcon.png")
icon_png = (OUT / "AppIcon.png").read_bytes()
ico_header = struct.pack("<HHH", 0, 1, 1)
ico_entry = struct.pack("<BBBBHHII", 0, 0, 0, 0, 1, 32, len(icon_png), 22)
(OUT / "App.ico").write_bytes(ico_header + ico_entry + icon_png)
print(f"Generated {len(sizes) + 2} neutral-glass assets in {OUT}")
