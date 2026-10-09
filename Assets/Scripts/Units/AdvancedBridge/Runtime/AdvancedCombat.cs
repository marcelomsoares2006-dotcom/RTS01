using System.Collections.Generic;
using UnityEngine;

/// <summary>Consultas de combate compartilhadas pelas unidades avancadas (sem estado).</summary>
public static class AdvancedCombat
{
    public static UnitManager ManagerOf(Transform t)
    {
        return t != null ? t.GetComponentInParent<UnitManager>() : null;
    }

    public static bool IsAlive(UnitManager m)
    {
        return m != null && m.Unit != null && m.Unit.HP > 0;
    }

    public static int IdOf(UnitManager m)
    {
        return m.GetHashCode(); // UnityEngine.Object.GetHashCode e estavel durante a vida do objeto (GetInstanceID e obsoleto no Unity 6)
    }

    /// <summary>Unidades e construcoes (camada UNIT_MASK) cujo colisor toca a esfera. Sem duplicatas.</summary>
    public static List<UnitManager> UnitsInRadius(Vector3 center, float radius)
    {
        List<UnitManager> result = new List<UnitManager>();
        HashSet<UnitManager> seen = new HashSet<UnitManager>();
        Collider[] colliders = Physics.OverlapSphere(center, radius, Globals.UNIT_MASK);
        foreach (Collider c in colliders)
        {
            UnitManager m = c.GetComponentInParent<UnitManager>();
            if (m != null && seen.Add(m)) result.Add(m);
        }
        return result;
    }

    public static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>Metade do maior lado da area ocupada pelo alvo (mesma conta de CheckUnitInRange).</summary>
    public static float TargetSize(UnitManager m)
    {
        if (m == null) return 0f;
        Vector3 s = m.MeshSize;
        return Mathf.Max(s.x, s.z);
    }
}
