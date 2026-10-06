using System;
using System.Collections.Generic;
using UnityEngine;

namespace EraImperial.Units.Advanced
{
    /// <summary>
    /// Referência a um asset visual que ainda pode não existir. O caminho esperado apenas documenta o destino;
    /// a referência só conta como pronta quando o objeto real está atribuído (asset != null, usando a
    /// comparação do Unity, que também trata referências quebradas/destruídas como ausentes).
    /// </summary>
    [Serializable]
    public sealed class VisualAssetReference
    {
        public string role;
        public string expectedAssetPath;
        public UnityEngine.Object asset;
        public bool required = true;

        public VisualAssetReference() { }

        public VisualAssetReference(string role, string expectedAssetPath, bool required = true)
        {
            this.role = role;
            this.expectedAssetPath = expectedAssetPath;
            this.required = required;
        }

        public bool IsResolved => asset != null;
    }

    /// <summary>Ponto de ancoragem exigido no prefab (arma, mão, centro da aura...). Só é verificável contra um prefab real.</summary>
    [Serializable]
    public sealed class SocketRequirement
    {
        public string socketName;
        /// <summary>Caminho relativo à raiz do prefab, no formato de Transform.Find.</summary>
        public string expectedTransformPath;
        public bool required = true;

        public SocketRequirement() { }

        public SocketRequirement(string socketName, string expectedTransformPath, bool required = true)
        {
            this.socketName = socketName;
            this.expectedTransformPath = expectedTransformPath;
            this.required = required;
        }

        public bool IsBoundTo(GameObject prefabRoot)
        {
            return prefabRoot != null
                && !string.IsNullOrEmpty(expectedTransformPath)
                && prefabRoot.transform.Find(expectedTransformPath) != null;
        }
    }

    /// <summary>Animação exigida. Os parâmetros seguem o Animator já usado pela árvore de comportamento (Running, Attacking, Hit).</summary>
    [Serializable]
    public sealed class AnimationRequirement
    {
        public string state;
        public string animatorParameter;
        public AnimationClip clip;
        public bool required = true;

        public AnimationRequirement() { }

        public AnimationRequirement(string state, string animatorParameter, bool required = true)
        {
            this.state = state;
            this.animatorParameter = animatorParameter;
            this.required = required;
        }

        public bool IsResolved => clip != null;
    }

    public sealed class VisualReadinessReport
    {
        private readonly List<string> _missing = new List<string>();

        public IReadOnlyList<string> Missing => _missing;
        public bool IsReady => _missing.Count == 0;

        public void AddMissing(string item) { _missing.Add(item); }
        public void AddRange(IEnumerable<string> items) { _missing.AddRange(items); }

        public override string ToString()
        {
            return IsReady ? "pronto" : "pendente: " + string.Join("; ", _missing);
        }
    }

    /// <summary>
    /// Requisitos visuais de uma unidade: prefab, malhas, efeitos, sockets e animações.
    /// Nada aqui cria malha, prefab ou material; o contrato apenas declara o que falta e
    /// só reporta "pronto" quando todos os itens obrigatórios apontam para assets reais.
    /// </summary>
    [Serializable]
    public sealed class AdvancedVisualContract
    {
        public VisualAssetReference prefab = new VisualAssetReference("prefab", "");
        public VisualAssetReference[] meshes = new VisualAssetReference[0];
        public VisualAssetReference[] effects = new VisualAssetReference[0];
        public SocketRequirement[] sockets = new SocketRequirement[0];
        public AnimationRequirement[] animations = new AnimationRequirement[0];

        public VisualReadinessReport Evaluate()
        {
            VisualReadinessReport report = new VisualReadinessReport();

            if (prefab == null || !prefab.IsResolved || !(prefab.asset is GameObject))
                report.AddMissing(Describe("prefab", prefab));

            AddMissingReferences(report, meshes);
            AddMissingReferences(report, effects);

            GameObject prefabRoot = prefab != null ? prefab.asset as GameObject : null;
            if (sockets != null)
            {
                foreach (SocketRequirement socket in sockets)
                {
                    if (socket == null || !socket.required) continue;
                    if (!socket.IsBoundTo(prefabRoot))
                        report.AddMissing($"socket '{socket.socketName}' ({socket.expectedTransformPath}) não verificado em um prefab real");
                }
            }

            if (animations != null)
            {
                foreach (AnimationRequirement animation in animations)
                {
                    if (animation == null || !animation.required) continue;
                    if (!animation.IsResolved)
                        report.AddMissing($"animação '{animation.state}' (parâmetro {animation.animatorParameter}) sem AnimationClip");
                }
            }

            return report;
        }

        private static void AddMissingReferences(VisualReadinessReport report, VisualAssetReference[] references)
        {
            if (references == null) return;
            foreach (VisualAssetReference reference in references)
            {
                if (reference == null || !reference.required) continue;
                if (!reference.IsResolved)
                    report.AddMissing(Describe(reference.role, reference));
            }
        }

        private static string Describe(string role, VisualAssetReference reference)
        {
            string expected = reference != null && !string.IsNullOrEmpty(reference.expectedAssetPath)
                ? reference.expectedAssetPath
                : "sem caminho definido";
            return $"{role} ausente ({expected})";
        }
    }
}
