using UnityEngine;
using UnityEngine.UI;

// Anahtarın "açık" katmanını değerle eşler; SetIsOnWithoutNotify ve kayıtlı değer olay tetiklemediği için her kare bakar.
[RequireComponent(typeof(Toggle))]
public class UiToggleView : MonoBehaviour
{
    [SerializeField] GameObject on;

    Toggle toggle;

    void OnEnable()
    {
        toggle = GetComponent<Toggle>();
        on.SetActive(toggle.isOn);
    }

    void LateUpdate()
    {
        if (on.activeSelf != toggle.isOn)
            on.SetActive(toggle.isOn);
    }
}
