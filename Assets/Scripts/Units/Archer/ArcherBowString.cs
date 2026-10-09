using UnityEngine;

/// <summary>
/// Corda do arco desenhada com um LineRenderer entre as duas pontas. Quando ha flecha encaixada, o ponto
/// central segue a mao que puxa; ao soltar, volta a posicao de repouso.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public sealed class ArcherBowString : MonoBehaviour
{
    public Transform tipTop;
    public Transform tipBottom;
    public Transform restPoint;
    public Transform pullPoint;
    [Range(0f, 1f)] public float pull;
    public float releaseSpeed = 14f;
    public float drawSpeed = 3f;

    private LineRenderer _line;
    private bool _drawing;

    public bool Drawing { get => _drawing; set => _drawing = value; }

    private void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _line.positionCount = 3;
        _line.useWorldSpace = true;
    }

    private void LateUpdate()
    {
        if (tipTop == null || tipBottom == null) return;
        float target = _drawing && pullPoint != null ? 1f : 0f;
        pull = Mathf.MoveTowards(pull, target, Time.deltaTime * (target > pull ? drawSpeed : releaseSpeed));
        Vector3 rest = restPoint != null ? restPoint.position : (tipTop.position + tipBottom.position) * 0.5f;
        Vector3 middle = pullPoint != null ? Vector3.Lerp(rest, pullPoint.position, pull) : rest;
        _line.SetPosition(0, tipTop.position);
        _line.SetPosition(1, middle);
        _line.SetPosition(2, tipBottom.position);
    }
}
