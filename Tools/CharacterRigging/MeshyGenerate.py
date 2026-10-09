"""Gera um personagem humanoide na Meshy (texto->imagem, imagem->3D, auto-rig) e guarda as entradas do pipeline.

A chave vem SOMENTE da variavel de ambiente MESHY_API_KEY; nunca e gravada. Os JSONs de tarefa salvos tem as
query strings assinadas das URLs removidas.

Uso (a partir da raiz do projeto):
  python Tools/CharacterRigging/MeshyGenerate.py image  <Nome>   # texto->imagem  -> Inputs/<Nome>/ref1.png (3 creditos)
  python Tools/CharacterRigging/MeshyGenerate.py model  <Nome>   # imagem->3D + rig + clipes Walk/Run (~35 creditos)
Os prompts ficam em PROMPTS abaixo. 'model' so roda depois de 'image' e da revisao visual da referencia.
"""
import json
import os
import sys
import time
import urllib.request
from pathlib import Path

API = "https://api.meshy.ai/openapi"
ROOT = Path(__file__).resolve().parents[2]
INPUTS = ROOT / "Tools/CharacterRigging/Inputs"

A_POSE = ("front view, standing in a strict A-pose with arms straight and held about 35 degrees away from the body, "
          "hands open and empty, fingers straight, feet shoulder-width apart, symmetrical.")
TAIL = (" Plain light grey background, even studio lighting, photorealistic, whole body visible head to boots.")

PROMPTS = {
    "CombatMage": dict(height=1.76, prompt=(
        "Full-body character reference of a realistic medieval battle mage, " + A_POSE +
        " Wearing a deep indigo knee-length wool robe with a high collar and bronze trim, a wide leather belt with "
        "small pouches, one bronze-trimmed shoulder pauldron, dark trousers and tall leather boots, hood down, short "
        "grey hair, trimmed beard, lean man. Nothing carried in hands, no staff, no cape, nothing on the back." + TAIL)),
    "PaladinHealer": dict(height=1.85, prompt=(
        "Full-body character reference of a realistic medieval paladin knight, " + A_POSE +
        " Wearing polished steel plate armor with a white linen surcoat bearing a golden cross on the chest, belted "
        "at the waist, steel gauntlets, greaves and sabatons, an open-faced sallet helmet with the face visible, "
        "short blond hair, clean-shaven. No cape, no sword, no shield, no weapon on the belt, nothing carried in "
        "hands, nothing on the back." + TAIL)),
}


def call(method, path, body=None):
    key = os.environ.get("MESHY_API_KEY")
    if not key:
        sys.exit("MESHY_API_KEY nao definida no ambiente.")
    data = json.dumps(body).encode() if body is not None else None
    request = urllib.request.Request(API + path, data=data, method=method,
                                     headers={"Authorization": "Bearer " + key, "Content-Type": "application/json"})
    with urllib.request.urlopen(request, timeout=60) as response:
        return json.loads(response.read())


def wait(path, label, timeout=1500):
    start = time.time()
    while True:
        task = call("GET", path)
        status = task.get("status")
        print(f"[{label}] {status} {task.get('progress', 0)}%", flush=True)
        if status == "SUCCEEDED":
            return task
        if status in ("FAILED", "CANCELED"):
            sys.exit(f"{label} falhou: {task.get('task_error')}")
        if time.time() - start > timeout:
            sys.exit(f"{label}: tempo esgotado")
        time.sleep(10)


def strip_urls(value):
    if isinstance(value, dict):
        return {k: strip_urls(v) for k, v in value.items()}
    if isinstance(value, list):
        return [strip_urls(v) for v in value]
    if isinstance(value, str) and value.startswith("http"):
        return value.split("?")[0]
    return value


def save_json(path, data):
    path.write_text(json.dumps(strip_urls(data), indent=1), encoding="utf-8")


def download(url, path):
    with urllib.request.urlopen(url, timeout=300) as response:
        path.write_bytes(response.read())
    print(f"baixado {path.name} ({path.stat().st_size // 1024} KB)")


def step_image(name):
    out = INPUTS / name
    out.mkdir(parents=True, exist_ok=True)
    spec = PROMPTS[name]
    body = {"ai_model": "nano-banana", "prompt": spec["prompt"]}
    save_json(out / "img1.json", body)
    task_id = call("POST", "/v1/text-to-image", body)["result"]
    task = wait(f"/v1/text-to-image/{task_id}", "imagem")
    save_json(out / "img1_task.json", task)
    download(task["image_urls"][0], out / "ref1.png")
    (out / "ref1.url.txt").write_text(task["image_urls"][0], encoding="utf-8")


def step_model(name):
    out = INPUTS / name
    spec = PROMPTS[name]
    image_url = (out / "ref1.url.txt").read_text(encoding="utf-8").strip()
    body = {"image_url": image_url, "ai_model": "latest", "model_type": "standard", "pose_mode": "a-pose",
            "should_remesh": True, "target_polycount": 30000, "topology": "triangle", "symmetry_mode": "on",
            "should_texture": True, "enable_pbr": True}
    save_json(out / "i3d.json", body)
    i3d_id = call("POST", "/v1/image-to-3d", body)["result"]
    i3d = wait(f"/v1/image-to-3d/{i3d_id}", "imagem->3D")
    save_json(out / "i3d_task.json", i3d)
    rig_body = {"input_task_id": i3d_id, "height_meters": spec["height"]}
    save_json(out / "rig.json", rig_body)
    rig_id = call("POST", "/v1/rigging", rig_body)["result"]
    rig = wait(f"/v1/rigging/{rig_id}", "rig")
    save_json(out / "rig_task.json", rig)
    result = rig["result"]
    name_l = name.lower()
    download(result["rigged_character_glb_url"], out / f"{name_l}_rigged.glb")
    basic = result["basic_animations"]
    download(basic["walking_glb_url"], out / "walking.glb")
    download(basic["running_glb_url"], out / "running.glb")
    textures = i3d.get("texture_urls") or []
    if textures and textures[0].get("normal"):
        download(textures[0]["normal"], out / "tex_normal.png")
    print("MODELO_OK", name)


if __name__ == "__main__":
    if len(sys.argv) != 3 or sys.argv[1] not in ("image", "model") or sys.argv[2] not in PROMPTS:
        sys.exit(__doc__)
    (step_image if sys.argv[1] == "image" else step_model)(sys.argv[2])
