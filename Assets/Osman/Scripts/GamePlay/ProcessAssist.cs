using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

// İşleme yardımı: ıstakadan taş sürüklerken bu taşın işlenebileceği yuvalar hafifçe parlar,
// taş bir per alanına yaklaşınca o alan yumuşakça büyür ve taşın düşeceği yuva güçlü parlar.
// Bırakınca o yuva hedef alınır. Kural kararı yine ScoreManager.ProcessManualDrop'ta.
// Yuvalara çocuk eklenmez: kod "dolu mu" diye childCount'a bakıyor; parıltılar ayrı bir katmanda çizilir.
public class ProcessAssist : MonoBehaviour
{
    const float ZoomScale = 1.5f;
    const float FocusRadius = 220f; // per alanının büyümesi için en yakın uygun yuvaya uzaklık
    const float TargetRadius = 90f; // taşın o yuvaya düşmesi için uzaklık
    const float Speed = 10f;

    static ProcessAssist instance;

    class Slot
    {
        public Placeholder ph;
        public Transform container; // "<NickName> meld"
        public Image glow;
    }

    readonly List<Slot> slots = new List<Slot>();
    readonly Dictionary<Transform, Vector3> baseScale = new Dictionary<Transform, Vector3>();
    readonly Dictionary<Transform, Vector3> basePos = new Dictionary<Transform, Vector3>();
    readonly Dictionary<Transform, int> baseSibling = new Dictionary<Transform, int>();
    readonly Stack<Image> pool = new Stack<Image>();
    Transform layer;
    TileUI dragged;
    Transform focus;
    Slot target;
    static Sprite whiteSprite;

    static ProcessAssist Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("ProcessAssist").AddComponent<ProcessAssist>();
            return instance;
        }
    }

    // Sürükleme başında: bu taşın işlenebileceği yuvaları bul ve hafifçe parlat.
    public static void Begin(TileUI tile)
    {
        var a = Instance;
        a.Clear();
        if (!CanProcess() || tile.tileDataInfo == null)
            return;
        a.dragged = tile;
        a.Collect(tile.tileDataInfo);
    }

    // Sürüklerken: yakındaki per alanını seç, hedef yuvayı bul.
    public static void Drag(TileUI tile)
    {
        var a = instance;
        if (a == null || a.dragged != tile || a.slots.Count == 0)
            return;
        Vector3 p = tile.transform.position;
        Slot nearest = null;
        float best = float.MaxValue;
        foreach (var s in a.slots)
        {
            if (s.ph == null)
                continue;
            float d = Vector2.Distance(s.ph.transform.position, p);
            if (d < best)
            {
                best = d;
                nearest = s;
            }
        }
        a.SetFocus(nearest != null && best < FocusRadius ? nearest.container : null);
        a.target = nearest != null && best < TargetRadius ? nearest : null;
    }

    // Bırakınca: parıltıları kaldır, alanı küçült, hedef yuvayı döndür (yoksa null).
    public static Placeholder End(TileUI tile)
    {
        var a = instance;
        if (a == null || a.dragged != tile)
            return null;
        Placeholder result = a.target?.ph;
        a.Clear();
        return result;
    }

    static bool CanProcess()
    {
        var gm = GameManager.Instance;
        var tm = FindObjectOfType<TurnManager>();
        var sm = gm != null ? gm.scoreManager : null;
        return tm != null
            && sm != null
            && tm.IsPlayerTurn()
            && tm.canDrop
            && (sm.hasOpenedSeries || sm.hasOpenedPairs);
    }

    void Collect(Tiles t)
    {
        var sm = GameManager.Instance.scoreManager;
        foreach (var player in PhotonNetwork.PlayerList)
        {
            GameObject go = GameObject.Find(player.NickName + " meld");
            if (go == null)
                continue;
            Transform container = go.transform;
            for (int type = 0; type < container.childCount && type < 3; type++)
            {
                foreach (Transform slot in container.GetChild(type))
                {
                    var ph = slot.GetComponent<Placeholder>();
                    if (ph != null && Fits(sm, ph, (ScoreManager.MeldType)type, t))
                        slots.Add(new Slot { ph = ph, container = container });
                }
            }
        }
        if (slots.Count == 0)
            return;
        EnsureLayer(slots[0].container.parent);
        foreach (var s in slots)
            s.glow = Glow();
    }

    // ProcessManualDrop'un ön kontrollerinin yan etkisiz kopyası; yalnızca parlatmak için.
    static bool Fits(ScoreManager sm, Placeholder ph, ScoreManager.MeldType type, Tiles t)
    {
        if (!ph.available || ph.AvailableTileInfo == null)
            return false;
        Tiles req = ph.AvailableTileInfo;
        bool occupied = ph.transform.childCount > 0;
        if (occupied)
        {
            var existing = ph.transform.GetChild(0).GetComponent<TileUI>();
            if (existing == null || !sm.IsSwappable(existing.tileDataInfo))
                return false;
            if (t.type == TileType.Joker)
                return false; // okey verip okey almak yok
        }
        else if (t.type == TileType.Joker)
            return true;

        if (type == ScoreManager.MeldType.MultiColor)
        {
            if (occupied && ScoreManager.FilledInGroupRow(ph.transform) < 4)
                return false;
            return t.number == req.number
                && !ScoreManager.GetUsedColorsInMultiColorRow(ph.transform).Contains(t.color);
        }
        return t.color == req.color && t.number == req.number;
    }

    void SetFocus(Transform c)
    {
        if (c == focus)
            return;
        if (c != null && !baseScale.ContainsKey(c))
        {
            baseScale[c] = c.localScale;
            basePos[c] = c.position;
            baseSibling[c] = c.GetSiblingIndex();
        }
        focus = c;
        // Büyüyen alan diğer per alanlarının üstünde çizilsin; parıltı katmanı en üstte kalsın.
        if (c != null)
        {
            c.SetAsLastSibling();
            layer.SetAsLastSibling();
        }
    }

    void Update()
    {
        float k = 1f - Mathf.Exp(-Speed * Time.deltaTime);
        // Büyüt / geri küçült; büyüyen alan ekrandan taşmasın diye içeri kaydırılır.
        var done = new List<Transform>();
        foreach (var kv in baseScale)
        {
            Transform c = kv.Key;
            if (c == null)
            {
                done.Add(c);
                continue;
            }
            bool on = c == focus;
            Vector3 wantScale = on ? kv.Value * ZoomScale : kv.Value;
            c.localScale = Vector3.Lerp(c.localScale, wantScale, k);
            Vector3 wantPos = on ? basePos[c] + KeepOnScreen(c) : basePos[c];
            c.position = Vector3.Lerp(c.position, wantPos, k);
            if (!on && (c.localScale - kv.Value).sqrMagnitude < 1e-6f)
            {
                c.localScale = kv.Value;
                c.position = basePos[c];
                c.SetSiblingIndex(baseSibling[c]);
                done.Add(c);
            }
        }
        foreach (var c in done)
        {
            baseScale.Remove(c);
            basePos.Remove(c);
            baseSibling.Remove(c);
        }

        // Parıltılar yuvayı izler (alan büyürken yuva da büyür ve kayar).
        float pulse = 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 8f);
        foreach (var s in slots)
        {
            if (s.ph == null || s.glow == null)
                continue;
            var rt = (RectTransform)s.ph.transform;
            var g = s.glow.rectTransform;
            g.position = rt.position;
            Vector3 ls = rt.lossyScale;
            Vector3 ps = g.parent.lossyScale;
            g.sizeDelta = new Vector2(rt.rect.width * ls.x / ps.x, rt.rect.height * ls.y / ps.y) * 1.15f;
            s.glow.color = s == target ? new Color(1f, 0.85f, 0.2f, pulse) : new Color(1f, 1f, 1f, 0.22f);
        }
    }

    // Büyümüş alanın dünya köşeleri kök canvas'ın dışına taşıyorsa, içeri itmek için gereken kayma.
    static Vector3 KeepOnScreen(Transform c)
    {
        var rt = c as RectTransform;
        var canvas = c.GetComponentInParent<Canvas>()?.rootCanvas;
        if (rt == null || canvas == null)
            return Vector3.zero;
        var mine = new Vector3[4];
        var screen = new Vector3[4];
        rt.GetWorldCorners(mine);
        ((RectTransform)canvas.transform).GetWorldCorners(screen);
        // Hedef ölçekteki köşeler, alanın asıl yerine göre (pivot ortada); animasyonun o anki halinden bağımsız.
        float grow = ZoomScale * (instance.baseScale[c].x / Mathf.Max(c.localScale.x, 1e-4f));
        Vector3 half = (mine[2] - mine[0]) * 0.5f * grow;
        Vector3 center = instance.basePos[c];
        Vector3 min = center - half;
        Vector3 max = center + half;
        Vector3 shift = Vector3.zero;
        if (min.x < screen[0].x)
            shift.x = screen[0].x - min.x;
        else if (max.x > screen[2].x)
            shift.x = screen[2].x - max.x;
        if (min.y < screen[0].y)
            shift.y = screen[0].y - min.y;
        else if (max.y > screen[2].y)
            shift.y = screen[2].y - max.y;
        return shift;
    }

    void EnsureLayer(Transform parent)
    {
        if (layer == null)
        {
            layer = new GameObject("ProcessGlowLayer", typeof(RectTransform)).transform;
            layer.SetParent(parent, false);
        }
        layer.SetAsLastSibling();
    }

    Image Glow()
    {
        if (whiteSprite == null)
            whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        Image img = pool.Count > 0 ? pool.Pop() : null;
        if (img == null)
        {
            img = new GameObject("ProcessGlow", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(layer, false);
            img.sprite = whiteSprite;
            img.raycastTarget = false;
        }
        img.gameObject.SetActive(true);
        return img;
    }

    void Clear()
    {
        foreach (var s in slots)
        {
            if (s.glow != null)
            {
                s.glow.gameObject.SetActive(false);
                pool.Push(s.glow);
            }
        }
        slots.Clear();
        target = null;
        dragged = null;
        focus = null; // Update büyümüş alanı yumuşakça eski haline döndürür
    }
}
