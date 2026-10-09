"""Monta folhas de contato (PIL, Python do sistema): MontagePoses.py <pasta> <prefixo-clip> <saida.png>"""
import sys, glob, re
from PIL import Image, ImageDraw
folder, clip, out = sys.argv[1:4]
prefix = sys.argv[4] if len(sys.argv) > 4 else "poses"
files = sorted(glob.glob(f"{folder}/{prefix}_{clip}_*_front.png"))
frames = [re.search(r"_(\d{3})_front", f).group(1) for f in files]
imgs = []
for fr in frames:
    row = []
    for v in ("front", "side"):
        im = Image.open(f"{folder}/{prefix}_{clip}_{fr}_{v}.png").convert("RGB")
        row.append(im)
    imgs.append((fr, row))
w, h = imgs[0][1][0].size
sheet = Image.new("RGB", (w * len(imgs), h * 2), (200, 200, 200))
d = ImageDraw.Draw(sheet)
for i, (fr, row) in enumerate(imgs):
    for j, im in enumerate(row):
        sheet.paste(im, (i * w, j * h))
    d.text((i * w + 8, 8), f"{clip} f{fr}", fill=(0, 0, 0))
sheet.save(out)
print("ok", out, sheet.size)
