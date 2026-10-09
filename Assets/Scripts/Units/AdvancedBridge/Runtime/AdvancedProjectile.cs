using System;
using EraImperial.Units.Advanced;
using UnityEngine;

/// <summary>
/// Projetil generico das unidades avancadas, dirigido por um ProjectileContract: voo reto (virote, esfera magica)
/// ou balistico em arco (pedra da catapulta). O dano nao vive aqui: o chamador recebe o ponto/alvo de impacto.
/// O prefab precisa ser real (asset visual atribuido ao contrato); sem prefab nada e disparado.
/// </summary>
public sealed class AdvancedProjectile : MonoBehaviour
{
    public static int ActiveCount { get; private set; }
    public static int ImpactCount { get; private set; }

    private ProjectileContract _contract;
    private Transform _target;
    private Vector3 _origin;
    private Vector3 _end;
    private Vector3 _lastKnownTarget;
    private Vector3 _targetOffset;
    private float _elapsed;
    private float _duration;
    private Action<Vector3, Transform> _onImpact;
    private bool _done;
    private const float HitRadius = 0.45f;

    public static AdvancedProjectile Spawn(
        GameObject prefab,
        Vector3 origin,
        Vector3 aimPoint,
        Transform target,
        ProjectileContract contract,
        Action<Vector3, Transform> onImpact)
    {
        if (prefab == null || contract == null)
        {
            Debug.LogError("AdvancedProjectile: prefab ou contrato ausente; nenhum projetil foi disparado.");
            return null;
        }

        GameObject go = UnityEngine.Object.Instantiate(prefab, origin, Quaternion.identity);
        go.name = prefab.name;
        AdvancedProjectile projectile = go.GetComponent<AdvancedProjectile>();
        if (projectile == null) projectile = go.AddComponent<AdvancedProjectile>();
        projectile.Launch(origin, aimPoint, target, contract, onImpact);
        return projectile;
    }

    private void Launch(Vector3 origin, Vector3 aimPoint, Transform target, ProjectileContract contract, Action<Vector3, Transform> onImpact)
    {
        _contract = contract;
        _target = target;
        _origin = origin;
        _end = aimPoint;
        _lastKnownTarget = aimPoint;
        _targetOffset = Vector3.up * 0.9f;
        _onImpact = onImpact;
        _elapsed = 0f;
        float distance = Vector3.Distance(origin, aimPoint);
        _duration = Mathf.Max(0.05f, contract.FlightTime(distance));
        ActiveCount++;
        Face(aimPoint - origin);
    }

    private void Update()
    {
        if (_done || _contract == null) return;
        _elapsed += Time.deltaTime;

        if (_contract.ballistic) UpdateBallistic();
        else UpdateStraight();

        if (!_done && _elapsed >= Mathf.Max(_contract.maxLifetime, _duration * 1.5f)) Impact(transform.position);
    }

    private void UpdateStraight()
    {
        if (_target != null) _lastKnownTarget = _target.position + _targetOffset;
        Vector3 toTarget = _lastKnownTarget - transform.position;
        float step = _contract.speed * Time.deltaTime;
        if (toTarget.magnitude <= Mathf.Max(step, HitRadius))
        {
            transform.position = _lastKnownTarget;
            Impact(_lastKnownTarget);
            return;
        }
        Face(toTarget);
        transform.position += toTarget.normalized * step;
    }

    private void UpdateBallistic()
    {
        float t = Mathf.Clamp01(_elapsed / _duration);
        Vector3 flat = Vector3.Lerp(_origin, _end, t);
        Vector3 position = flat + Vector3.up * (4f * _contract.arcHeight * t * (1f - t));
        Face(position - transform.position);
        transform.position = position;
        if (t >= 1f) Impact(_end);
    }

    private void Face(Vector3 direction)
    {
        if (direction.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(direction.normalized);
    }

    private void Impact(Vector3 point)
    {
        if (_done) return;
        _done = true;
        ActiveCount = Mathf.Max(0, ActiveCount - 1);
        ImpactCount++;

        _onImpact?.Invoke(point, _target);

        GameObject effect = _contract.impactEffect != null ? _contract.impactEffect.asset as GameObject : null;
        if (effect != null)
        {
            GameObject instance = Instantiate(effect, point, Quaternion.identity);
            Destroy(instance, 4f);
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (!_done && _contract != null) ActiveCount = Mathf.Max(0, ActiveCount - 1);
    }
}
