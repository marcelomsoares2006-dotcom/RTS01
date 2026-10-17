"""Gera mapas PBR determinísticos para o único piloto de produção House.

Os mapas são autoria procedural de produção do piloto, não são cópias dos mapas
do showcase. O script escreve somente em Assets/Resources/Textures/Buildings/HousePilotPBR.
"""
from __future__ import annotations

import math
from pathlib import Path

import bpy


ROOT = Path.cwd()
OUT = ROOT / "Assets" / "Resources" / "Textures" / "Buildings" / "HousePilotPBR"
SIZE = 512


def clamp(value: float) -> float:
    return max(0.0, min(1.0, value))


def noise(x: int, y: int, seed: int) -> float:
    value = (x * 374761393 + y * 668265263 + seed * 982451653) & 0xFFFFFFFF
    value = (value ^ (value >> 13)) * 1274126177 & 0xFFFFFFFF
    return ((value ^ (value >> 16)) & 0xFFFFFFFF) / 4294967295.0 - 0.5


def height(x: int, y: int, profile: str) -> float:
    if profile == "Wood":
        grain = math.sin((x * 0.075 + y * 0.012) * 1.0)
        knots = math.sin(x * 0.017 + y * 0.031) * 0.035
        return clamp(0.5 + grain * 0.13 + knots + noise(x, y, 37) * 0.08)
    tile_x = x % 64
    tile_y = (y + (x // 64 % 2) * 20) % 48
    edge = -0.16 if tile_x < 3 or tile_y < 3 else 0.0
    return clamp(0.5 + edge + math.sin((x + y * 0.3) * 0.08) * 0.05 + noise(x, y, 51) * 0.05)


def save(name: str, pixels: list[float], *, srgb: bool) -> None:
    image = bpy.data.images.get(name) or bpy.data.images.new(name, width=SIZE, height=SIZE, alpha=True)
    image.pixels = pixels
    image.filepath_raw = str(OUT / f"{name}.png")
    image.file_format = "PNG"
    image.save()


def generate(profile: str) -> None:
    albedo: list[float] = []
    normal: list[float] = []
    metallic_gloss: list[float] = []
    ao: list[float] = []
    if profile == "Wood":
        base = (0.32, 0.14, 0.045)
        high = (0.62, 0.31, 0.10)
        metallic, smoothness, seed = 0.0, 0.24, 37
    else:
        base = (0.055, 0.07, 0.085)
        high = (0.20, 0.23, 0.25)
        metallic, smoothness, seed = 0.02, 0.30, 51

    for y in range(SIZE):
        for x in range(SIZE):
            h = height(x, y, profile)
            tint = clamp((h - 0.30) / 0.45)
            grain_noise = noise(x, y, seed) * 0.035
            albedo.extend([
                clamp(base[0] + (high[0] - base[0]) * tint + grain_noise),
                clamp(base[1] + (high[1] - base[1]) * tint + grain_noise),
                clamp(base[2] + (high[2] - base[2]) * tint + grain_noise),
                1.0,
            ])
            hx = height((x + 1) % SIZE, y, profile) - height((x - 1) % SIZE, y, profile)
            hy = height(x, (y + 1) % SIZE, profile) - height(x, (y - 1) % SIZE, profile)
            nx, ny, nz = -hx * 3.0, -hy * 3.0, 1.0
            length = math.sqrt(nx * nx + ny * ny + nz * nz)
            normal.extend([nx / length * 0.5 + 0.5, ny / length * 0.5 + 0.5, nz / length * 0.5 + 0.5, 1.0])
            cavity = clamp(0.93 + (h - 0.5) * 0.18)
            metallic_gloss.extend([metallic, metallic, metallic, smoothness])
            ao.extend([cavity, cavity, cavity, 1.0])

    save(f"HousePilot_{profile}_Albedo", albedo, srgb=True)
    save(f"HousePilot_{profile}_Normal", normal, srgb=False)
    save(f"HousePilot_{profile}_MetallicGloss", metallic_gloss, srgb=False)
    save(f"HousePilot_{profile}_AO", ao, srgb=False)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    generate("Wood")
    generate("Roof")
    print(f"HOUSE_PILOT_PBR_MAPS_GENERATED maps=8 out={OUT}")


if __name__ == "__main__":
    main()
