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

    // Kart listesi değişince ya da sürükleme bitince çağrılır; yerleşim bir kare sonra hesaplanır.
    public void Settle()
    {
        if (!isActiveAndEnabled || dragging)
            return;
        Stop();
        snap = StartCoroutine(SnapNextFrame());
    }

    void Stop()
    {
        if (snap != null)
            StopCoroutine(snap);
        snap = null;
    }

    IEnumerator SnapNextFrame()
    {
        yield return null;
        RectTransform content = scroll.content;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        int count = 0;
        foreach (Transform c in content)
            if (c.gameObject.activeSelf)
                count++;
        scroll.horizontal = count > 1;
        scroll.StopMovement();
        float from = content.anchoredPosition.x;
        float to = count <= 1 ? 0f : Nearest(content, from);
        for (float t = 0f; t < Duration; t += Time.unscaledDeltaTime)
        {
            float k = 1f - (1f - t / Duration) * (1f - t / Duration);
            content.anchoredPosition = new Vector2(Mathf.Lerp(from, to, k), content.anchoredPosition.y);
            yield return null;
        }
        content.anchoredPosition = new Vector2(to, content.anchoredPosition.y);
        snap = null;
    }

    // Kart merkezi görünüm ortasına gelince içeriğin x konumu: yarım genişlik - kartın içerikteki x'i.
    float Nearest(RectTransform content, float x)
    {
        float half = scroll.viewport.rect.width / 2f;
        float best = x, bestDist = float.MaxValue;
        foreach (Transform c in content)
        {
            if (!c.gameObject.activeSelf)
                continue;
            float target = half - c.localPosition.x;
            float d = Mathf.Abs(target - x);
            if (d < bestDist) { bestDist = d; best = target; }
        }
        float min = Mathf.Min(0f, scroll.viewport.rect.width - content.rect.width);
        return Mathf.Clamp(best, min, 0f);
    }
}
