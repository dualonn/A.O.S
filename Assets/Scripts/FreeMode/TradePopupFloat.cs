using TMPro;
using UnityEngine;

/// <summary>Floats a popup up and fades it out.</summary>
public class TradePopupFloat : MonoBehaviour
{
    private float age;
    private TextMeshPro tmp;

    private void Awake() => tmp = GetComponent<TextMeshPro>();

    private void Update()
    {
        age += Time.deltaTime;
        transform.position += Vector3.up * Time.deltaTime * 0.6f;
        if (tmp != null) tmp.alpha = Mathf.Clamp01(2f - age);
        if (age > 2f) Destroy(gameObject);
    }
}
