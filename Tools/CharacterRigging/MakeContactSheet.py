"""Monta uma folha de contato a partir de PNGs. Uso: python MakeContactSheet.py saida.png colunas largura_celula arq1.png arq2.png ..."""
import sys
from PIL import Image, ImageDraw

out, cols, cw = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
files = sys.argv[4:]
imgs = [Image.open(f).convert('RGB') for f in files]
ch = int(cw * imgs[0].height / imgs[0].width)
rows = (len(imgs) + cols - 1) // cols
sheet = Image.new('RGB', (cols * cw, rows * ch), (20, 20, 20))
d = ImageDraw.Draw(sheet)
for i, (im, f) in enumerate(zip(imgs, files)):
    t = im.resize((cw, ch), Image.LANCZOS)
    x, y = (i % cols) * cw, (i // cols) * ch
    sheet.paste(t, (x, y))
    d.text((x + 6, y + 4), f.replace('\\', '/').split('/')[-1][:-4], fill=(255, 255, 0))
sheet.save(out)
