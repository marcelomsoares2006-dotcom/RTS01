using UnityEngine;

namespace EraImperial.Units.Advanced
{
    /// <summary>
    /// Contrato de projétil, separado da unidade que o dispara. Descreve o voo e o efeito de impacto;
    /// o prefab do projétil só conta como pronto quando um GameObject real for atribuído.
    /// O dano continua definido pela unidade/era (o projétil apenas transporta o tipo de dano).
    /// </summary>
    [CreateAssetMenu(fileName = "ProjectileContract", menuName = "Era Imperial/Units/Projectile Contract")]
    public sealed class ProjectileContract : ScriptableObject
    {
        public string code;
        public DamageType damageType = DamageType.Physical;
        [Min(0f)] public float speed = 18f;
        [Min(0f)] public float maxLifetime = 6f;
        /// <summary>Trajetória em arco (catapulta); falso = reta (virote, bola de energia).</summary>
        public bool ballistic;
        [Min(0f)] public float arcHeight;
        public VisualAssetReference prefab = new VisualAssetReference("projectile prefab", "");
        public VisualAssetReference impactEffect = new VisualAssetReference("impact effect", "");

        public ValidationReport Validate()
        {
            ValidationReport report = new ValidationReport();
            if (string.IsNullOrWhiteSpace(code)) report.Add("projétil sem código");
            if (speed <= 0f) report.Add($"projétil '{code}': velocidade deve ser > 0");
            if (maxLifetime <= 0f) report.Add($"projétil '{code}': tempo de vida deve ser > 0");
            if (ballistic && arcHeight <= 0f) report.Add($"projétil '{code}': trajetória balística exige arcHeight > 0");
            return report;
        }

        public VisualReadinessReport EvaluateVisuals()
        {
            VisualReadinessReport report = new VisualReadinessReport();
            if (prefab == null || !prefab.IsResolved || !(prefab.asset is GameObject))
                report.AddMissing($"projétil '{code}': prefab ausente ({(prefab != null ? prefab.expectedAssetPath : "")})");
            if (impactEffect != null && impactEffect.required && !impactEffect.IsResolved)
                report.AddMissing($"projétil '{code}': efeito de impacto ausente ({impactEffect.expectedAssetPath})");
            return report;
        }

        /// <summary>Tempo de voo em linha reta até a distância informada; 0 se a velocidade for inválida.</summary>
        public float FlightTime(float distance)
        {
            return speed > 0f && distance > 0f ? distance / speed : 0f;
        }
    }
}
