# Maakt assets/Dusk.ico en assets/Dusk-256.png: het logo (schemerring) op een donkere tegel.
# Gebruik: python build/make_icon.py   (heeft Pillow nodig)
import math, os
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "assets")
N = 1024                      # eerst groot tekenen, dan verkleinen (gladde randen)
K = N / 48.0 * 0.78           # logo op 78% van de tegel
OFF = (N - 48 * K) / 2

def P(x, y):
    return (OFF + x * K, OFF + y * K)

def gradient():
    # oranje linksonder, roze, paars rechtsboven (zoals in het ontwerp)
    a, b = P(8, 40), P(40, 12)
    stops = [(0.0, (251, 146, 60)), (0.55, (232, 121, 249)), (1.0, (129, 140, 248))]
    img = Image.new("RGB", (N, N))
    px = img.load()
    dx, dy = b[0] - a[0], b[1] - a[1]
    L = dx * dx + dy * dy
    for y in range(N):
        for x in range(N):
            t = ((x - a[0]) * dx + (y - a[1]) * dy) / L
            t = max(0.0, min(1.0, t))
            for i in range(len(stops) - 1):
                t0, c0 = stops[i]; t1, c1 = stops[i + 1]
                if t <= t1:
                    u = (t - t0) / (t1 - t0)
                    px[x, y] = tuple(int(c0[j] + (c1[j] - c0[j]) * u) for j in range(3))
                    break
    return img

def main():
    tile = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(tile)
    d.rounded_rectangle([0, 0, N - 1, N - 1], radius=int(N * 0.23), fill=(11, 16, 32, 255))

    w = 4.5 * K
    cx, cy = P(24, 25.78)
    r = 15.5 * K
    mask = Image.new("L", (N, N), 0)
    md = ImageDraw.Draw(mask)
    start, sweep = -58.9, 297.8
    box = [cx - r - w / 2, cy - r - w / 2, cx + r + w / 2, cy + r + w / 2]
    md.arc(box, start, start + sweep, fill=255, width=int(w))
    for ang in (start, start + sweep):   # ronde uiteinden
        ex, ey = cx + r * math.cos(math.radians(ang)), cy + r * math.sin(math.radians(ang))
        md.ellipse([ex - w / 2, ey - w / 2, ex + w / 2, ey + w / 2], fill=255)
    grad = gradient().convert("RGBA")
    tile.paste(grad, (0, 0), mask)

    bar = (224, 231, 255, 255)
    x0, y0 = P(24, 7); x1, y1 = P(24, 21)
    d.line([x0, y0, x1, y1], fill=bar, width=int(w))
    for (ex, ey) in ((x0, y0), (x1, y1)):
        d.ellipse([ex - w / 2, ey - w / 2, ex + w / 2, ey + w / 2], fill=bar)

    os.makedirs(OUT, exist_ok=True)
    big = tile.resize((256, 256), Image.LANCZOS)
    big.save(os.path.join(OUT, "Dusk-256.png"))
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    tile.resize((256, 256), Image.LANCZOS).save(os.path.join(OUT, "Dusk.ico"), sizes=[(s, s) for s in sizes])
    print("klaar")

if __name__ == "__main__":
    main()
