from pathlib import Path
import math
import struct
import zlib

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "ServerScheduler" / "Assets"
OUT.mkdir(parents=True, exist_ok=True)


def png_bytes(width, height, pixels):
    raw = b"".join(b"\x00" + bytes(pixels[y * width * 4:(y + 1) * width * 4]) for y in range(height))
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xffffffff)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")


def rounded_rect(px, w, h, box, radius, top, bottom=None):
    x0, y0, x1, y1 = [int(round(v)) for v in box]
    r = max(0, int(round(radius)))
    bottom = bottom or top
    for y in range(max(0, y0), min(h, y1)):
        t = (y - y0) / max(1, y1 - y0 - 1)
        color = [round(top[i] * (1 - t) + bottom[i] * t) for i in range(3)] + [255]
        for x in range(max(0, x0), min(w, x1)):
            cx = min(max(x, x0 + r), x1 - 1 - r)
            cy = min(max(y, y0 + r), y1 - 1 - r)
            if (x - cx) ** 2 + (y - cy) ** 2 <= r ** 2:
                i = (y * w + x) * 4
                px[i:i + 4] = color


def rect(px, w, h, box, color):
    x0, y0, x1, y1 = [int(round(v)) for v in box]
    for y in range(max(0, y0), min(h, y1)):
        for x in range(max(0, x0), min(w, x1)):
            i = (y * w + x) * 4
            px[i:i + 4] = list(color)


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


def make_icon(width, height, path):
    scale = 3
    w, h = width * scale, height * scale
    px = bytearray(w * h * 4)
    side = min(w, h)
    cx, cy = w // 2, h // 2
    size = side * 0.82
    x0, y0 = cx - size / 2, cy - size / 2
    x1, y1 = cx + size / 2, cy + size / 2
    rounded_rect(px, w, h, (x0, y0, x1, y1), size * 0.22, (10, 103, 255), (65, 128, 245))

    cal_w = size * 0.53
    cal_h = size * 0.50
    left, top = cx - cal_w / 2, cy - cal_h / 2 + size * 0.025
    right, bottom = cx + cal_w / 2, cy + cal_h / 2
    rounded_rect(px, w, h, (left, top, right, bottom), size * 0.055, (250, 252, 255), (226, 237, 255))
    rounded_rect(px, w, h, (left, top, right, top + cal_h * 0.28), size * 0.055, (255, 255, 255))
    rect(px, w, h, (left, top + cal_h * 0.20, right, top + cal_h * 0.28), (255, 255, 255, 255))

    ring_w = cal_w * 0.075
    for rx in (left + cal_w * 0.27, left + cal_w * 0.73):
        rounded_rect(px, w, h, (rx - ring_w / 2, top - cal_h * 0.11, rx + ring_w / 2, top + cal_h * 0.15), ring_w / 2, (255, 255, 255))

    cell = cal_w * 0.12
    gap = cal_w * 0.09
    for row in range(2):
        for col in range(3):
            bx = left + cal_w * 0.17 + col * (cell + gap)
            by = top + cal_h * 0.42 + row * (cell + gap * 0.65)
            rounded_rect(px, w, h, (bx, by, bx + cell, by + cell), cell * 0.22, (46, 111, 233))

    small = downsample(px, w, h, scale)
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
print(f"Generated {len(sizes) + 2} assets in {OUT}")
