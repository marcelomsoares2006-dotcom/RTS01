using UnityEngine;

namespace EraImperial.CharacterPreparation
{
    public class Stage2RigDemo : MonoBehaviour
    {
        public Animator[] characters;
        private bool paused;
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16, 16, 500, 125), GUI.skin.box);
            GUILayout.Label("Etapa 2 — Farmhand e Spearman");
            GUILayout.Label("PoseCheck: teste de rig e articulações; não é animação final de jogo.");
            if (GUILayout.Button(paused ? "Continuar poses" : "Pausar poses"))
            {
                paused = !paused;
                foreach (var character in characters) character.speed = paused ? 0f : 0.7f;
            }
            if (GUILayout.Button("Pose inicial"))
                foreach (var character in characters) { character.Play("PoseCheck", 0, 0); character.Update(0); }
            GUILayout.EndArea();
        }
    }
}
