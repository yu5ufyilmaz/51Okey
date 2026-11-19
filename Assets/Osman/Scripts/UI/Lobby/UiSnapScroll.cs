using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Yatay kart listesi: sürükleme bitince en yakın kart ortaya kayar. Yan dolgu ilk ve son kartı ortalayacak kadardır.
// İçerik sola çapalı (pivot x = 0); kart sayısı değişince konum korunur, yalnız en yakın karta düzeltilir.
// shortcut: kartlar ölçeklenmez; ortadaki kartı büyütmek istenirse burada ölçek eklenir
[RequireComponent(typeof(ScrollRect))]
public class UiSnapScroll : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    const float Duration = 0.2f;

    [SerializeField] float itemWidth = 1000f;

    ScrollRect scroll;
    HorizontalLayoutGroup layout;
    Coroutine snap;
    bool dragging;
    float lastWidth = -1f;

    void Awake()
    {
        scroll = GetComponent<ScrollRect>();
        layout = scroll.content.GetComponent<HorizontalLayoutGroup>();
        scroll.inertia = false;
    }

    void LateUpdate()
    {
        float w = scroll.viewport.rect.width;
        if (Mathf.Approximately(w, lastWidth))
            return;
        lastWidth = w;
        int pad = Mathf.Max(0, Mathf.RoundToInt((w - itemWidth) / 2f));
        layout.padding.left = layout.padding.right = pad;
        LayoutRebuilder.MarkLayoutForRebuild(scroll.content);
        Settle();
    }

    public void OnBeginDrag(PointerEventData e)
    {
        dragging = true;
        Stop();
    }

    public void OnEndDrag(PointerEventData e)
    {
        dragging = false;
        Settle();
    }

    // Kart listesi değişince ya da sürükleme bitince çağrılır. keep verilirse o kart beklemeden ortaya konur
    // (önüne kart girip çıkınca parmağın altındaki masa değişmesin); yoksa en yakın kart yumuşakça ortaya gelir.
    public void Settle(Transform keep = null)
    {
        if (!isActiveAndEnabled || dragging)
            return;
        Stop();
        RectTransform content = scroll.content;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content); // kaldırılan kartlar kapatıldı, bu karede sayılmaz
        int count = 0;
        foreach (Transform c in content)
            if (c.gameObject.activeSelf)
                count++;
        scroll.horizontal = count > 1;
        scroll.StopMovement();
        if (count <= 1)
            SetX(0f);
        else if (keep != null && keep.gameObject.activeSelf)
            SetX(Clamp(Target(keep)));
        else
            snap = StartCoroutine(Ease(content.anchoredPosition.x, Nearest(content.anchoredPosition.x)));
    }

    // Görünüm ortasına en yakın kart (yoksa null).
    public Transform Centred()
    {
        if (scroll == null)
            return null;
        Transform best = null;
        float bestDist = float.MaxValue;
        float x = scroll.content.anchoredPosition.x;
        foreach (Transform c in scroll.content)
        {
            if (!c.gameObject.activeSelf)
                continue;
            float d = Mathf.Abs(Target(c) - x);
            if (d < bestDist) { bestDist = d; best = c; }
        }
        return best;
    }

    void Stop()
    {
        if (snap != null)
            StopCoroutine(snap);
        snap = null;
    }

    IEnumerator Ease(float from, float to)
    {
        for (float t = 0f; t < Duration; t += Time.unscaledDeltaTime)
        {
            float k = 1f - (1f - t / Duration) * (1f - t / Duration);
            SetX(Mathf.Lerp(from, to, k));
            yield return null;
        }
        SetX(to);
        snap = null;
    }

    void SetX(float x) => scroll.content.anchoredPosition = new Vector2(x, scroll.content.anchoredPosition.y);

    // Kart merkezi görünüm ortasına gelince içeriğin x konumu: yarım genişlik - kartın içerikteki x'i.
    float Target(Transform card) => scroll.viewport.rect.width / 2f - card.localPosition.x;

    float Clamp(float x) => Mathf.Clamp(x, Mathf.Min(0f, scroll.viewport.rect.width - scroll.content.rect.width), 0f);

    float Nearest(float x)
    {
        Transform c = Centred();
        return c == null ? x : Clamp(Target(c));
    }
}
