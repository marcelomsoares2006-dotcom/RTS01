"""Reuse the tested Stage2 human FK pipeline for two independent armoured variants."""
from pathlib import Path
import importlib.util
root=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('human_pipeline',Path(__file__).with_name('BuildStage2Characters.py'))
human=importlib.util.module_from_spec(spec);spec.loader.exec_module(human)
human.WORK=root/'Tools/CharacterRigging/Working/Stage5'
human.OUT=root/'Assets/CharacterRigging/Stage5'
human.REPORT=root/'Docs/CharacterMeshAudit/Stage5'
human.CONFIG={
 'ArmoredKnight':('*Armored_Knight_*.blend',22000),
 'DarkKnight':('*villain_Dark_Knigh*.blend',22000),
}
human.main()
