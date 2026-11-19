using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Giriş ekranındaki "Kullanım Şartları / Gizlilik Politikası" bağlantıları; adresler boşken metin hiç görünmez.
[RequireComponent(typeof(TMP_Text))]
public class LegalLinksText : MonoBehaviour, IPointerClickHandler
{
    void Awake() => gameObject.SetActive(GameConfig.HasLegalLinks);

    public void OnPointerClick(PointerEventData e)
    {
        var text = GetComponent<TMP_Text>();
        // Canvas Screen Space Overlay: kamera null
        int i = TMP_TextUtilities.FindIntersectingLink(text, e.position, null);
        if (i < 0)
            return;
        var url = GameConfig.LegalUrl(text.textInfo.linkInfo[i].GetLinkID());
        if (url.Length > 0)
            Application.OpenURL(url);
    }
}
