using UnityEngine;

/// <summary>
/// Runtime bridge from a stable military prefab to an authored Resources model.
/// The primitive Mesh child remains a safe fallback until Unity imports the OBJ.
/// </summary>
[DisallowMultipleComponent]
public sealed class MilitaryUnitVisualBinding : MonoBehaviour
{
    [SerializeField] private string modelResourcePath;
    [SerializeField] private string authoredVisualName = "AuthoredVisual";
    [SerializeField] private bool hideProxyWhenLoaded = true;
    [SerializeField] private Vector3 authoredLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 authoredLocalScale = Vector3.one;

    private GameObject _authoredVisual;

    private void Awake()
    {
        TryLoadAuthoredVisual();
    }

    private void TryLoadAuthoredVisual()
    {
        if (string.IsNullOrWhiteSpace(modelResourcePath)) return;
        GameObject source = Resources.Load<GameObject>(modelResourcePath);
        if (source == null) return;

        _authoredVisual = Instantiate(source, transform);
        _authoredVisual.name = string.IsNullOrWhiteSpace(authoredVisualName) ? source.name : authoredVisualName;
        _authoredVisual.transform.localPosition = authoredLocalPosition;
        _authoredVisual.transform.localRotation = Quaternion.identity;
        _authoredVisual.transform.localScale = authoredLocalScale;

        if (!hideProxyWhenLoaded) return;
        Transform proxy = transform.Find("Mesh");
        if (proxy == null) return;
        foreach (Renderer renderer in proxy.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
    }

    private void OnDestroy()
    {
        if (_authoredVisual != null) Destroy(_authoredVisual);
    }
}
