"""生成 ServerFlow 图标：高对比、深色底、粗笔画，保证 16-48px 可辨识。

输出：
  ServerScheduler/Assets/App.ico            多尺寸 16/20/24/32/40/48/64/128/256
  ServerScheduler/Assets/AppIcon.png        256
  ServerScheduler/Assets/Square44x44Logo.png / 71 / 150 / 310 / Wide310x150 / StoreLogo / SplashScreen
"""

from PIL import Image, ImageChops, ImageDraw

S = 1024  # 超采样画布

BLUE_TOP = (59, 123, 246)
BLUE_BOTTOM = (26, 68, 196)
HEADER = (16, 52, 158)
WHITE = (255, 255, 255)
ACCENT = (43, 107, 243)


def vertical_gradient(size, top, bottom):
    img = Image.new("RGB", (1, size))
    for y in range(size):
        t = y / max(1, size - 1)
        img.putpixel((0, y), tuple(round(top[i] + (bottom[i] - top[i]) * t) for i in range(3)))
    return img.resize((size, size))


def rounded_mask(size, radius, box=None):
    mask = Image.new("L", (size, size), 0)
    draw = ImageDraw.Draw(mask)
    draw.rounded_rectangle(box or (0, 0, size - 1, size - 1), radius=radius, fill=255)
    return mask


def render(size=S):
    """返回 size×size 的 RGBA 图。"""
    base = vertical_gradient(size, BLUE_TOP, BLUE_BOTTOM).convert("RGBA")

    # 背景圆角方块（满幅），22.5% 半径 —— Windows 应用图标惯例
    bg_mask = rounded_mask(size, round(size * 0.225))
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    canvas.paste(base, (0, 0), bg_mask)

    draw = ImageDraw.Draw(canvas)

    # 日历本体
    left, right = round(size * 0.185), round(size * 0.815)
    top, bottom = round(size * 0.255), round(size * 0.805)
    body_radius = round(size * 0.075)
    body_box = (left, top, right, bottom)
    draw.rounded_rectangle(body_box, radius=body_radius, fill=WHITE)

    # 表头深色带：与白色表体形成两段式，16px 仍能看出“日历”
    header_h = round(size * 0.14)
    body_mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(body_mask).rounded_rectangle(body_box, radius=body_radius, fill=255)
    band_mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(band_mask).rectangle((left, top, right, top + header_h), fill=255)
    header_mask = ImageChops.multiply(body_mask, band_mask)
    canvas.paste(Image.new("RGBA", (size, size), HEADER), (0, 0), header_mask)

    # 两枚装订环（白），穿过表头
    ring_w = round(size * 0.062)
    for cx in (0.375, 0.625):
        cx_px = round(size * cx)
        draw.rounded_rectangle(
            (cx_px - ring_w // 2, round(size * 0.15), cx_px + ring_w // 2, round(size * 0.33)),
            radius=ring_w // 2,
            fill=WHITE,
        )

    # 选中的那一格
    cell = round(size * 0.185)
    cy = round(size * 0.615)
    draw.rounded_rectangle(
        (size // 2 - cell // 2, cy - cell // 2, size // 2 + cell // 2, cy + cell // 2),
        radius=round(size * 0.042),
        fill=ACCENT,
    )

    return canvas


def scaled(px):
    """按目标像素尺寸渲染（直接从超采样图缩放，边缘干净）。"""
    return render(S).resize((px, px), Image.LANCZOS)


def main():
    master = render(S)

    master.resize((256, 256), Image.LANCZOS).save("ServerScheduler/Assets/AppIcon.png")

    frames = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    images = [master.resize((px, px), Image.LANCZOS) for px in frames]
    images[-1].save(
        "ServerScheduler/Assets/App.ico",
        format="ICO",
        sizes=[(px, px) for px in frames],
        append_images=images[:-1],
    )

    def tile(path, w, h, ratio):
        canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        inner = round(min(w, h) * ratio)
        icon = master.resize((inner, inner), Image.LANCZOS)
        canvas.paste(icon, ((w - inner) // 2, (h - inner) // 2), icon)
        canvas.save(path)

    tile("ServerScheduler/Assets/Square44x44Logo.png", 44, 44, 0.92)
    tile("ServerScheduler/Assets/Square71x71Logo.png", 71, 71, 0.86)
    tile("ServerScheduler/Assets/Square150x150Logo.png", 150, 150, 0.72)
    tile("ServerScheduler/Assets/Square310x310Logo.png", 310, 310, 0.66)
    tile("ServerScheduler/Assets/Wide310x150Logo.png", 310, 150, 0.74)
    tile("ServerScheduler/Assets/StoreLogo.png", 50, 50, 1.0)

    splash = Image.new("RGB", (620, 300), (243, 247, 252))
    icon = master.resize((132, 132), Image.LANCZOS)
    splash.paste(icon, ((620 - 132) // 2, (300 - 132) // 2), icon)
    splash.save("ServerScheduler/Assets/SplashScreen.png")

    print("icon assets written")


if __name__ == "__main__":
    main()
