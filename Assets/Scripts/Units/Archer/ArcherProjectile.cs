using UnityEngine;

/// <summary>
/// Flecha em voo: arco balistico simples ate o alvo (segue o alvo enquanto existir) e dano ao chegar.
/// O dano passa por UnitManager.TakeHit quando o alvo e uma unidade do jogo; caso contrario usa
/// SendMessage("TakeHit", int) para alvos de teste.
/// </summary>
public sealed class ArcherProjectile : MonoBehaviour
{
    public const float StickSeconds = 2f;

    private Vector3 _start;
    private Vector3 _endPoint;
    private Transform _target;
    private Vector3 _targetOffset;
    private float _duration;
    private float _elapsed;
    private float _arc;
    private int _damage;
    private Vector3 _previous;
    private bool _done;

    public bool Done => _done;

    public static ArcherProjectile Spawn(GameObject prefab, Vector3 origin, Transform target, float speed, int damage)
    {
        Vector3 offset = AimOffset(target);
        GameObject go = prefab != null ? Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "ArcherArrow";
        go.transform.position = origin;
        ArcherProjectile projectile = go.GetComponent<ArcherProjectile>();
        if (projectile == null) projectile = go.AddComponent<ArcherProjectile>();
        projectile.Launch(origin, target, offset, speed, damage);
        return projectile;
    }

    /// <summary>Ponto de mira relativo ao pivo do alvo (centro do collider, ou 1 m acima do chao).</summary>
    public static Vector3 AimOffset(Transform target)
    {
        if (target == null) return Vector3.up;
        Collider c = target.GetComponentInChildren<Collider>();
        return c != null ? c.bounds.center - target.position : Vector3.up;
    }

    public void Launch(Vector3 origin, Transform target, Vector3 targetOffset, float speed, int damage)
    {
        _start = origin;
        _target = target;
        _targetOffset = targetOffset;
        _endPoint = (target != null ? target.position : origin + transform.forward * 10f) + targetOffset;
        _damage = damage;
        float distance = Vector3.Distance(_start, _endPoint);
        _duration = Mathf.Max(0.05f, distance / Mathf.Max(0.1f, speed));
        _arc = Mathf.Clamp(distance * 0.08f, 0.1f, 1.5f);
        _previous = origin;
        _elapsed = 0f;
        _done = false;
        enabled = true;
    }

    private void Update()
    {
        if (_done) return;
        if (_target != null) _endPoint = _target.position + _targetOffset;

        _elapsed += Time.deltaTime;
        float p = Mathf.Clamp01(_elapsed / _duration);
        Vector3 position = Vector3.Lerp(_start, _endPoint, p) + Vector3.up * (_arc * 4f * p * (1f - p));
        Vector3 direction = position - _previous;
        if (direction.sqrMagnitude > 1e-8f) transform.rotation = Quaternion.LookRotation(direction.normalized);
        transform.position = position;
        _previous = position;

        if (p >= 1f) Hit();
    }

    private void Hit()
    {
        _done = true;
        enabled = false;
        if (_target != null)
        {
            UnitManager manager = _target.GetComponentInParent<UnitManager>();
            if (manager != null) manager.TakeHit(_damage);
            else _target.SendMessage("TakeHit", _damage, SendMessageOptions.DontRequireReceiver);
            if (_target != null) transform.SetParent(_target, true);
        }
        Destroy(gameObject, StickSeconds);
    }
}
