"""图标方案比较：三个变体 × 多种尺寸，用于挑选后再落地。"""

from PIL import Image, ImageChops, ImageDraw, ImageFilter

S = 2048

BLUE_LIGHT = (77, 141, 255)
BLUE_DEEP = (23, 55, 165)
ACCENT = (37, 99, 235)
INK = (17, 47, 122)
WHITE = (255, 255, 255)


def gradient(size, top, bottom):
    img = Image.new("RGB", (1, size))
    for y in range(size):
        t = y / max(1, size - 1)
        img.putpixel((0, y), tuple(round(top[i] + (bottom[i] - top[i]) * t) for i in range(3)))
    return img.resize((size, size))


def premultiplied_resize(img, px):
    """按预乘 alpha 缩放，避免半透明边缘出现黑边。"""
    r, g, b, a = img.split()
    rp = ImageChops.multiply(r, a)
    gp = ImageChops.multiply(g, a)
    bp = ImageChops.multiply(b, a)
    pm = Image.merge("RGBA", (rp, gp, bp, a)).resize((px, px), Image.LANCZOS)
    r2, g2, b2, a2 = pm.split()

    def unmultiply(channel):
        return channel.point(lambda v: v)

    r3 = ImageChops.multiply(r2, a2.point(lambda v: 255 if v == 0 else min(255, int(255 * 255 / max(v, 1)))))
    g3 = ImageChops.multiply(g2, a2.point(lambda v: 255 if v == 0 else min(255, int(255 * 255 / max(v, 1)))))
    b3 = ImageChops.multiply(b2, a2.point(lambda v: 255 if v == 0 else min(255, int(255 * 255 / max(v, 1)))))
    return Image.merge("RGBA", (r3, g3, b3, a2))


def base_tile(size, radius_ratio=0.225):
    tile = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    body = gradient(size, BLUE_LIGHT, BLUE_DEEP).convert("RGBA")
    radius = round(size * radius_ratio)
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, size - 1, size - 1), radius=radius, fill=255)
    tile.paste(body, (0, 0), mask)

    # 左上柔光，避免纯渐变发死
    glow = Image.new("L", (size, size), 0)
    ImageDraw.Draw(glow).ellipse(
        (-size * 0.35, -size * 0.5, size * 0.85, size * 0.55), fill=64)
    glow = glow.filter(ImageFilter.GaussianBlur(size * 0.12))
    glow = ImageChops.multiply(glow, mask)
    tile.paste(Image.new("RGBA", (size, size), (255, 255, 255, 255)), (0, 0), glow)

    # 内描边，提升边缘精致度
    rim = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    rd = ImageDraw.Draw(rim)
    inset = round(size * 0.008)
    rd.rounded_rectangle(
        (inset, inset, size - 1 - inset, size - 1 - inset),
        radius=radius - inset, outline=(255, 255, 255, 40), width=max(2, round(size * 0.008)))
    tile.alpha_composite(rim)
    return tile, mask


def calendar_geo(size):
    left, right = round(size * 0.20), round(size * 0.80)
    top, bottom = round(size * 0.27), round(size * 0.79)
    return left, top, right, bottom


def variant_a(size):
    """A：白色日历 + 浅色表头 + 蓝色装订环（低对比、柔和）"""
    tile, _ = base_tile(size)
    left, top, right, bottom = calendar_geo(size)
    radius = round(size * 0.075)

    shadow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle(
        (left, top + round(size * 0.02), right, bottom + round(size * 0.02)),
        radius=radius, fill=(6, 24, 80, 90))
    shadow = shadow.filter(ImageFilter.GaussianBlur(size * 0.018))
    tile.alpha_composite(shadow)

    d = ImageDraw.Draw(tile)
    d.rounded_rectangle((left, top, right, bottom), radius=radius, fill=WHITE)

    header_h = round((bottom - top) * 0.26)
    body_mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(body_mask).rounded_rectangle((left, top, right, bottom), radius=radius, fill=255)
    band = Image.new("L", (size, size), 0)
    ImageDraw.Draw(band).rectangle((left, top, right, top + header_h), fill=255)
    tile.paste(Image.new("RGBA", (size, size), (228, 238, 255, 255)), (0, 0),
               ImageChops.multiply(body_mask, band))

    ring_w = round(size * 0.050)
    for cx in (0.355, 0.645):
        x = round(size * cx)
        d.rounded_rectangle(
            (x - ring_w // 2, round(size * 0.195), x + ring_w // 2, round(size * 0.345)),
            radius=ring_w // 2, fill=ACCENT)

    cell = round(size * 0.175)
    cx, cy = size // 2, round((top + header_h + bottom) / 2 + size * 0.015)
    d.rounded_rectangle((cx - cell // 2, cy - cell // 2, cx + cell // 2, cy + cell // 2),
                        radius=round(cell * 0.22), fill=ACCENT)
    return tile


def variant_b(size):
    """B：白色日历 + 蓝色表头 + 白色装订环（高对比）"""
    tile, _ = base_tile(size)
    left, top, right, bottom = calendar_geo(size)
    radius = round(size * 0.075)

    shadow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle(
        (left, top + round(size * 0.02), right, bottom + round(size * 0.02)),
        radius=radius, fill=(6, 24, 80, 90))
    shadow = shadow.filter(ImageFilter.GaussianBlur(size * 0.018))
    tile.alpha_composite(shadow)

    d = ImageDraw.Draw(tile)
    d.rounded_rectangle((left, top, right, bottom), radius=radius, fill=WHITE)

    header_h = round((bottom - top) * 0.24)
    body_mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(body_mask).rounded_rectangle((left, top, right, bottom), radius=radius, fill=255)
    band = Image.new("L", (size, size), 0)
    ImageDraw.Draw(band).rectangle((left, top, right, top + header_h), fill=255)
    tile.paste(Image.new("RGBA", (size, size), ACCENT + (255,)), (0, 0),
               ImageChops.multiply(body_mask, band))

    ring_w = round(size * 0.050)
    for cx in (0.355, 0.645):
        x = round(size * cx)
        d.rounded_rectangle(
            (x - ring_w // 2, round(size * 0.195), x + ring_w // 2, round(size * 0.345)),
            radius=ring_w // 2, fill=WHITE)

    cell = round(size * 0.17)
    cx, cy = size // 2, round((top + header_h + bottom) / 2 + size * 0.012)
    d.rounded_rectangle((cx - cell // 2, cy - cell // 2, cx + cell // 2, cy + cell // 2),
                        radius=round(cell * 0.2), fill=INK)
    return tile


def variant_c(size):
    """C：纯白日历轮廓 + 一格实心（极简，无表头带）"""
    tile, _ = base_tile(size)
    left, top, right, bottom = calendar_geo(size)
    radius = round(size * 0.085)

    shadow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle(
        (left, top + round(size * 0.02), right, bottom + round(size * 0.02)),
        radius=radius, fill=(6, 24, 80, 90))
    shadow = shadow.filter(ImageFilter.GaussianBlur(size * 0.018))
    tile.alpha_composite(shadow)

    d = ImageDraw.Draw(tile)
    d.rounded_rectangle((left, top, right, bottom), radius=radius, fill=WHITE)

    ring_w = round(size * 0.052)
    for cx in (0.355, 0.645):
        x = round(size * cx)
        d.rounded_rectangle(
            (x - ring_w // 2, round(size * 0.185), x + ring_w // 2, round(size * 0.335)),
            radius=ring_w // 2, fill=ACCENT)
        # 环与日历之间的缺口
        d.rectangle((x - ring_w // 2 - 1, round(size * 0.255), x + ring_w // 2 + 1, round(size * 0.30)),
                    fill=WHITE)

    cell = round(size * 0.20)
    cx, cy = size // 2, round(size * 0.615)
    d.rounded_rectangle((cx - cell // 2, cy - cell // 2, cx + cell // 2, cy + cell // 2),
                        radius=round(cell * 0.24), fill=ACCENT)
    return tile


def variant_d(size):
    """D：白色卡片剪影自带两个装订耳，表头用强调色，单格高亮"""
    tile, mask = base_tile(size)
    left, top, right, bottom = round(size * 0.205), round(size * 0.27), round(size * 0.795), round(size * 0.78)
    radius = round(size * 0.072)

    shadow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle(
        (left, top + round(size * 0.022), right, bottom + round(size * 0.022)),
        radius=radius, fill=(6, 24, 80, 100))
    shadow = shadow.filter(ImageFilter.GaussianBlur(size * 0.02))
    tile.alpha_composite(shadow)

    d = ImageDraw.Draw(tile)
    d.rounded_rectangle((left, top, right, bottom), radius=radius, fill=WHITE)

    header_h = round((bottom - top) * 0.25)
    body_mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(body_mask).rounded_rectangle((left, top, right, bottom), radius=radius, fill=255)
    band = Image.new("L", (size, size), 0)
    ImageDraw.Draw(band).rectangle((left, top, right, top + header_h), fill=255)
    tile.paste(Image.new("RGBA", (size, size), ACCENT + (255,)), (0, 0),
               ImageChops.multiply(body_mask, band))

    # 装订耳：白色圆头，顶部高出卡片，底部并入白色表体
    ear_w = round(size * 0.055)
    for cx in (0.352, 0.648):
        x = round(size * cx)
        d.rounded_rectangle(
            (x - ear_w // 2, round(size * 0.205), x + ear_w // 2, top + header_h + round(size * 0.03)),
            radius=ear_w // 2, fill=WHITE)

    cell = round(size * 0.195)
    cx, cy = size // 2, round((top + header_h + bottom) / 2 + size * 0.012)
    d.rounded_rectangle((cx - cell // 2, cy - cell // 2, cx + cell // 2, cy + cell // 2),
                        radius=round(cell * 0.3), fill=(29, 78, 216))

    # 卡片底部内阴影，增加体积
    inner = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(inner).rounded_rectangle(
        (left, round(bottom - size * 0.05), right, bottom), radius=radius, fill=(10, 30, 90, 26))
    inner = inner.filter(ImageFilter.GaussianBlur(size * 0.01))
    inner.putalpha(ImageChops.multiply(inner.split()[3], body_mask))
    tile.alpha_composite(inner)
    return tile


def variant_f(size):
    """F：白卡片 + 短装订扣（只高出卡片一点）+ 强调色表头 + 单格"""
    tile, _ = base_tile(size)
    left, right = round(size * 0.215), round(size * 0.785)
    top, bottom = round(size * 0.275), round(size * 0.765)
    radius = round(size * 0.085)

    shadow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle(
        (left, top + round(size * 0.02), right, bottom + round(size * 0.02)),
        radius=radius, fill=(8, 26, 84, 105))
    shadow = shadow.filter(ImageFilter.GaussianBlur(size * 0.02))
    tile.alpha_composite(shadow)

    # 装订扣：短、圆头，紧贴卡片上沿
    ear_w = round(size * 0.052)
    d = ImageDraw.Draw(tile)
    for cx in (0.35, 0.65):
        x = round(size * cx)
        d.rounded_rectangle((x - ear_w // 2, round(size * 0.225), x + ear_w // 2, round(size * 0.335)),
                            radius=ear_w // 2, fill=WHITE)

    d.rounded_rectangle((left, top, right, bottom), radius=radius, fill=WHITE)

    header_h = round((bottom - top) * 0.235)
    body_mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(body_mask).rounded_rectangle((left, top, right, bottom), radius=radius, fill=255)
    band = Image.new("L", (size, size), 0)
    ImageDraw.Draw(band).rectangle((left, top, right, top + header_h), fill=255)
    tile.paste(Image.new("RGBA", (size, size), ACCENT + (255,)), (0, 0),
               ImageChops.multiply(body_mask, band))

    cell = round(size * 0.185)
    cx, cy = size // 2, round((top + header_h + bottom) / 2 + size * 0.008)
    d.rounded_rectangle((cx - cell // 2, cy - cell // 2, cx + cell // 2, cy + cell // 2),
                        radius=round(cell * 0.26), fill=(23, 61, 186))
    return tile


def variant_g(size):
    """G：F 的收敛版——圆角更小、表头与单格同色"""
    tile, _ = base_tile(size, radius_ratio=0.175)
    left, right = round(size * 0.215), round(size * 0.785)
    top, bottom = round(size * 0.28), round(size * 0.765)
    radius = round(size * 0.072)

    shadow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle(
        (left, top + round(size * 0.02), right, bottom + round(size * 0.02)),
        radius=radius, fill=(8, 26, 84, 105))
    shadow = shadow.filter(ImageFilter.GaussianBlur(size * 0.02))
    tile.alpha_composite(shadow)

    ear_w = round(size * 0.050)
    d = ImageDraw.Draw(tile)
    for cx in (0.35, 0.65):
        x = round(size * cx)
        d.rounded_rectangle((x - ear_w // 2, round(size * 0.232), x + ear_w // 2, round(size * 0.335)),
                            radius=ear_w // 2, fill=WHITE)

    d.rounded_rectangle((left, top, right, bottom), radius=radius, fill=WHITE)

    header_h = round((bottom - top) * 0.235)
    body_mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(body_mask).rounded_rectangle((left, top, right, bottom), radius=radius, fill=255)
    band = Image.new("L", (size, size), 0)
    ImageDraw.Draw(band).rectangle((left, top, right, top + header_h), fill=255)
    tile.paste(Image.new("RGBA", (size, size), ACCENT + (255,)), (0, 0),
               ImageChops.multiply(body_mask, band))

    cell = round(size * 0.18)
    cx, cy = size // 2, round((top + header_h + bottom) / 2 + size * 0.006)
    d.rounded_rectangle((cx - cell // 2, cy - cell // 2, cx + cell // 2, cy + cell // 2),
                        radius=round(cell * 0.26), fill=ACCENT)
    return tile


VARIANTS = {"F": variant_f, "G": variant_g}





def main():
    sizes = [16, 24, 32, 48, 64]
    zoom = 8
    pad = 16
    cell_w = max(sizes) * zoom + pad * 2
    rows = len(VARIANTS)
    W = cell_w * len(sizes)
    H = rows * (max(sizes) * zoom + pad * 2) + 30
    sheet = Image.new("RGB", (W, H), (245, 246, 249))

    for r, (name, fn) in enumerate(VARIANTS.items()):
        master = fn(S)
        for c, px in enumerate(sizes):
            small = premultiplied_resize(master, px)
            big = small.resize((px * zoom, px * zoom), Image.NEAREST)
            x = c * cell_w + (cell_w - big.width) // 2
            y = 10 + r * (max(sizes) * zoom + pad * 2) + pad
            sheet.paste(big, (x, y), big)
        master.resize((256, 256), Image.LANCZOS).save(f"dist/variant-{name}.png")

    sheet.save("dist/variants.png")
    print("ok", sheet.size)


if __name__ == "__main__":
    main()
