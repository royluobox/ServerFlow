"""生成 ServerFlow 图标与 MSIX 磁贴资源。

设计：深蓝渐变圆角底 + 白色日历卡片 + 强调色表头与单格高亮，
笔画按 16 px 可辨设计；每个尺寸都由 2048 px 母版按预乘 alpha 缩放。
"""

from PIL import Image, ImageChops, ImageDraw, ImageFilter

S = 2048

BLUE_LIGHT = (77, 141, 255)
BLUE_DEEP = (23, 55, 165)
ACCENT = (37, 99, 235)
WHITE = (255, 255, 255)


def _gradient(size, top, bottom):
    img = Image.new("RGB", (1, size))
    for y in range(size):
        t = y / max(1, size - 1)
        img.putpixel((0, y), tuple(round(top[i] + (bottom[i] - top[i]) * t) for i in range(3)))
    return img.resize((size, size))


def _resize(img, px):
    """预乘 alpha 后再缩放，避免半透明边缘发黑。"""
    r, g, b, a = img.split()
    premultiplied = Image.merge(
        "RGBA",
        (ImageChops.multiply(r, a), ImageChops.multiply(g, a), ImageChops.multiply(b, a), a),
    ).resize((px, px), Image.LANCZOS)

    r2, g2, b2, a2 = premultiplied.split()
    scale = a2.point(lambda v: 255 if v == 0 else min(255, (255 * 255) // v))
    return Image.merge(
        "RGBA",
        (ImageChops.multiply(r2, scale), ImageChops.multiply(g2, scale), ImageChops.multiply(b2, scale), a2),
    )


def render(size=S):
    tile = Image.new("RGBA", (size, size), (0, 0, 0, 0))

    # 底：竖向渐变 + 左上柔光 + 内描边
    body = _gradient(size, BLUE_LIGHT, BLUE_DEEP).convert("RGBA")
    radius = round(size * 0.20)
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, size - 1, size - 1), radius=radius, fill=255)
    tile.paste(body, (0, 0), mask)

    glow = Image.new("L", (size, size), 0)
    ImageDraw.Draw(glow).ellipse((-size * 0.35, -size * 0.5, size * 0.85, size * 0.55), fill=64)
    glow = ImageChops.multiply(glow.filter(ImageFilter.GaussianBlur(size * 0.12)), mask)
    tile.paste(Image.new("RGBA", (size, size), WHITE + (255,)), (0, 0), glow)

    rim = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    inset = round(size * 0.008)
    ImageDraw.Draw(rim).rounded_rectangle(
        (inset, inset, size - 1 - inset, size - 1 - inset),
        radius=radius - inset,
        outline=(255, 255, 255, 40),
        width=max(2, round(size * 0.008)),
    )
    tile.alpha_composite(rim)

    # 日历卡片
    left, right = round(size * 0.235), round(size * 0.765)
    top, bottom = round(size * 0.295), round(size * 0.755)
    card_radius = round(size * 0.068)

    shadow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle(
        (left, top + round(size * 0.02), right, bottom + round(size * 0.02)),
        radius=card_radius, fill=(8, 26, 84, 70))
    tile.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(size * 0.026)))

    draw = ImageDraw.Draw(tile)

    # 装订扣：短圆头，只高出卡片一点
    ear_w = round(size * 0.046)
    for cx in (0.365, 0.635):
        x = round(size * cx)
        draw.rounded_rectangle(
            (x - ear_w // 2, round(size * 0.245), x + ear_w // 2, round(size * 0.335)),
            radius=ear_w // 2, fill=WHITE)

    draw.rounded_rectangle((left, top, right, bottom), radius=card_radius, fill=WHITE)

    # 表头
    header_h = round((bottom - top) * 0.22)
    card_mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(card_mask).rounded_rectangle((left, top, right, bottom), radius=card_radius, fill=255)
    band = Image.new("L", (size, size), 0)
    ImageDraw.Draw(band).rectangle((left, top, right, top + header_h), fill=255)
    tile.paste(Image.new("RGBA", (size, size), ACCENT + (255,)), (0, 0), ImageChops.multiply(card_mask, band))

    # 高亮的那一天
    cell = round(size * 0.165)
    cx = size // 2
    cy = round((top + header_h + bottom) / 2 + size * 0.004)
    draw.rounded_rectangle(
        (cx - cell // 2, cy - cell // 2, cx + cell // 2, cy + cell // 2),
        radius=round(cell * 0.26), fill=ACCENT)

    return tile


def main():
    master = render(S)

    _resize(master, 256).save("ServerScheduler/Assets/AppIcon.png")

    frames = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    images = [_resize(master, px) for px in frames]
    images[-1].save(
        "ServerScheduler/Assets/App.ico",
        format="ICO",
        sizes=[(px, px) for px in frames],
        append_images=images[:-1],
    )

    def tile(path, w, h, ratio):
        canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        inner = round(min(w, h) * ratio)
        icon = _resize(master, inner)
        canvas.paste(icon, ((w - inner) // 2, (h - inner) // 2), icon)
        canvas.save(path)

    tile("ServerScheduler/Assets/Square44x44Logo.png", 44, 44, 0.92)
    tile("ServerScheduler/Assets/Square71x71Logo.png", 71, 71, 0.86)
    tile("ServerScheduler/Assets/Square150x150Logo.png", 150, 150, 0.72)
    tile("ServerScheduler/Assets/Square310x310Logo.png", 310, 310, 0.66)
    tile("ServerScheduler/Assets/Wide310x150Logo.png", 310, 150, 0.74)
    tile("ServerScheduler/Assets/StoreLogo.png", 50, 50, 1.0)

    splash = Image.new("RGB", (620, 300), (243, 247, 252))
    icon = _resize(master, 132)
    splash.paste(icon, ((620 - 132) // 2, (300 - 132) // 2), icon)
    splash.save("ServerScheduler/Assets/SplashScreen.png")

    print("icon assets written")


if __name__ == "__main__":
    main()
