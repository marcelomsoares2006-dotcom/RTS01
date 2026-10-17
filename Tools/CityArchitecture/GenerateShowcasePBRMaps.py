"""Gera mapas PBR complementares apenas para os materiais do showcase.

Este script não abre nem salva .blend, FBX, prefab ou cena. Os mapas são
autoria procedural determinística para apresentação visual: não são bake de
produção e não devem ser promovidos automaticamente para gameplay.
"""
from __future__ import annotations

import math
import os
from pathlib import Path

import bpy


ROOT = Path.cwd()
OUT = ROOT / "Assets" / "CityArchitecture" / "Generated" / "Textures" / "Showcase"
SIZE = 512

PROFILES = {
    "Stone": {"metallic": 0.0, "smoothness": 0.20, "seed": 11, "scale": 0.9},
    "Plaster": {"metallic": 0.0, "smoothness": 0.28, "seed": 23, "scale": 1.1},
    "Wood": {"metallic": 0.0, "smoothness": 0.34, "seed": 37, "scale": 1.0},
    "Roof": {"metallic": 0.0, "smoothness": 0.40, "seed": 51, "scale": 1.2},
    "Trim": {"metallic": 0.72, "smoothness": 0.76, "seed": 67, "scale": 0.75},
}


def clamp(value: float, low: float = 0.0, high: float = 1.0) -> float:
    return max(low, min(high, value))


def hash_noise(x: int, y: int, seed: int) -> float:
    value = (x * 374761393 + y * 668265263 + seed * 982451653) & 0xFFFFFFFF
    value = (value ^ (value >> 13)) * 1274126177 & 0xFFFFFFFF
    return ((value ^ (value >> 16)) & 0xFFFFFFFF) / 4294967295.0


def height_at(x: int, y: int, profile: str) -> float:
    cfg = PROFILES[profile]
    u = x / SIZE
    v = y / SIZE
    n = hash_noise(x, y, cfg["seed"]) - 0.5
    if profile == "Stone":
        mortar = 0.0
        if (x % 64) < 3 or (y % 48) < 3:
            mortar = -0.20
        pattern = 0.06 * math.sin(x * 0.19) + 0.05 * math.sin(y * 0.23)
        return 0.50 + mortar + pattern + n * 0.10
    if profile == "Plaster":
        return 0.50 + 0.10 * math.sin(x * 0.06) * math.sin(y * 0.08) + n * 0.08
    if profile == "Wood":
        grain = math.sin((x * 0.11 + y * 0.018) * cfg["scale"])
        return 0.50 + grain * 0.12 + n * 0.07
    if profile == "Roof":
        tile = math.sin((x + y * 0.35) * 0.16 * cfg["scale"])
        return 0.50 + tile * tile * 0.12 + n * 0.06
    return 0.50 + 0.05 * math.sin(x * 0.14) + n * 0.04


def save_image(name: str, pixels: list[float], *, alpha: bool = True) -> None:
    image = bpy.data.images.get(name) or bpy.data.images.new(name, width=SIZE, height=SIZE, alpha=alpha)
    image.pixels = pixels
    image.filepath_raw = str(OUT / f"{name}.png")
    image.file_format = "PNG"
    image.save()


def generate(profile: str) -> None:
    cfg = PROFILES[profile]
    normal: list[float] = []
    metallic_gloss: list[float] = []
    ao: list[float] = []
    for y in range(SIZE):
        for x in range(SIZE):
            h = height_at(x, y, profile)
            hx = height_at((x + 1) % SIZE, y, profile) - height_at((x - 1) % SIZE, y, profile)
            hy = height_at(x, (y + 1) % SIZE, profile) - height_at(x, (y - 1) % SIZE, profile)
            nx, ny, nz = -hx * 3.4, -hy * 3.4, 1.0
            length = math.sqrt(nx * nx + ny * ny + nz * nz)
            nx, ny, nz = nx / length, ny / length, nz / length
            normal.extend([nx * 0.5 + 0.5, ny * 0.5 + 0.5, nz * 0.5 + 0.5, 1.0])

            edge_shadow = clamp(0.90 + (h - 0.5) * 0.22)
            metallic_gloss.extend([cfg["metallic"], cfg["metallic"], cfg["metallic"], cfg["smoothness"]])
            ao.extend([edge_shadow, edge_shadow, edge_shadow, 1.0])
    save_image(f"Showcase_{profile}_Normal", normal)
    save_image(f"Showcase_{profile}_MetallicGloss", metallic_gloss)
    save_image(f"Showcase_{profile}_AO", ao)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    for profile in PROFILES:
        generate(profile)
    print(f"SHOWCASE_PBR_MAPS_GENERATED profiles={len(PROFILES)} maps={len(PROFILES) * 3} out={OUT}")


if __name__ == "__main__":
    main()
