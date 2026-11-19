using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviourPunCallbacks
{
    public static UIManager Instance;

    [Header("Texts - Game Info")]
    public TextMeshProUGUI tileCountText;
    public TextMeshProUGUI tableLimitText;

    [Header("Texts - Player Stats")]
    public TextMeshProUGUI currentScoreText;
    public TextMeshProUGUI currentPairText;

    [Header("Visuals")]
    public GameObject[] turnIndicators; // 0:Me, 1:Right, 2:Top, 3:Left

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        ShowRackScores(0, 0, 0);
    }

    public void UpdateTileCount(int count)
    {
        if (tileCountText)
            tileCountText.text = count.ToString();
    }

    // Raf yazıları ilk güncellemeden önce sahnedeki Türkçe metinle kalmasın diye Start de çağırır.
    void ShowRackScores(int myScore, int myPairsScore, int currentLimit)
    {
        // 1. Seri Puanı
        if (currentScoreText != null)
        {
            string colorHex = (myScore > currentLimit) ? "green" : "white";
            currentScoreText.text = Loc.T("table.run_score", colorHex, myScore);
        }

        // 2. Çift Puanı (Dikkat: Buraya artık Puan geliyor)
        if (currentPairText != null)
        {
            string colorHex = (myPairsScore > currentLimit) ? "green" : "white";
            // İstersen burada "Çift Puanı" yazabilirsin
            currentPairText.text = Loc.T("table.pair_score", colorHex, myPairsScore);
        }
    }

    public void UpdatePlayerStats(int myScore, int myPairsScore, int currentLimit)
    {
        ShowRackScores(myScore, myPairsScore, currentLimit);

        // 3. Ortak Baraj
        if (tableLimitText != null)
        {
            tableLimitText.text = Loc.T("table.threshold", currentLimit);
        }

        // pairLimitText objesini silebilirsin veya boş bırakabilirsin.
    }

    public void UpdateTurnIndicators(int activePlayerQue)
    {
        int myQue = 0;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object val))
            myQue = (int)val;

        if (myQue == 0)
            return;

        int targetIndex = (activePlayerQue - myQue + 4) % 4;

        for (int i = 0; i < turnIndicators.Length; i++)
        {
            if (turnIndicators[i])
                turnIndicators[i].SetActive(i == targetIndex);
        }
    }

    // --- TUR SÜRESİ BARI: sırası gelenin isimliğinin alt şeridinde, süreyle soldan azalır, yeşil→sarı→kırmızı ---
    private TurnManager turnManager;
    private UnityEngine.UI.Image[] timerBars;
    private static Sprite whiteSprite;
    private static readonly Color barGreen = new Color(0.03f, 1f, 0f);
    private static readonly Color barYellow = new Color(1f, 0.85f, 0f);
    private static readonly Color barRed = new Color(1f, 0.2f, 0.15f);
    private float sfxPrevRemaining; // TimeLow kenar algılama

    private void Update()
    {
        if (turnManager == null)
            turnManager = FindObjectOfType<TurnManager>();
        var gm = GameManager.Instance;
        if (turnManager == null || gm == null || turnIndicators == null)
            return;
        int myQue = 0;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object val))
            myQue = (int)val;
        if (myQue == 0)
            return;

        bool running = gm.isGameReady && !gm.isGameEnded && turnManager.TurnStartTime > 0;
        int active = (turnManager.currentTurnPlayer - myQue + 4) % 4;
        float left = running ? turnManager.TurnRemaining / turnManager.TurnSeconds : 0f;
        float sfxRemaining = running ? turnManager.TurnRemaining : 0f;
        if (running && active == 0 && Sfx.CrossedBelow(sfxPrevRemaining, sfxRemaining, 5f))
            Sfx.Play(SfxId.TimeLow);
        sfxPrevRemaining = sfxRemaining;
        for (int i = 0; i < turnIndicators.Length; i++)
        {
            var bar = TimerBar(i);
            if (bar == null)
                continue;
            bool on = running && i == active;
            if (bar.gameObject.activeSelf != on)
                bar.gameObject.SetActive(on);
            if (!on)
                continue;
            bar.fillAmount = left;
            bar.color =
                left > 0.5f
                    ? Color.Lerp(barYellow, barGreen, (left - 0.5f) * 2f)
                    : Color.Lerp(barRed, barYellow, left * 2f);
        }
    }

    // İlk kullanımda isimliğin (kullanici_ui) alt koyu şeridine oturan bir dolgu bar kurulur.
    private UnityEngine.UI.Image TimerBar(int i)
    {
        if (timerBars == null)
            timerBars = new UnityEngine.UI.Image[turnIndicators.Length];
        if (timerBars[i] != null || turnIndicators[i] == null)
            return timerBars[i];
        if (whiteSprite == null)
            whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));

        var go = new GameObject("TurnTimerBar", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(turnIndicators[i].transform.parent, false);
        var rt = (RectTransform)go.transform;
        // Şerit, görselin alt %22'si; kenarlardan biraz içeride.
        rt.anchorMin = new Vector2(0.02f, 0.04f);
        rt.anchorMax = new Vector2(0.98f, 0.19f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<UnityEngine.UI.Image>();
        img.sprite = whiteSprite;
        img.type = UnityEngine.UI.Image.Type.Filled;
        img.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        img.fillOrigin = 0;
        img.raycastTarget = false;
        go.SetActive(false);
        timerBars[i] = img;
        return img;
    }

    // --- KISA BİLGİ MESAJI (ör. "limitin yetmiyor") ---
    private TextMeshProUGUI messageText;
    private Coroutine messageRoutine;

    public void ShowMessage(string message, float seconds = 2.5f)
    {
        if (messageText == null)
        {
            // Sahneye ayrıca eklemeye gerek kalmasın diye ilk kullanımda masa limiti yazısının canvas'ına kurulur.
            Canvas canvas = tableLimitText != null ? tableLimitText.canvas.rootCanvas : FindObjectOfType<Canvas>();
            if (canvas == null)
                return;
            messageText = new GameObject("MessageText", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            messageText.transform.SetParent(canvas.transform, false);
            if (tableLimitText != null)
                messageText.font = tableLimitText.font;
            messageText.alignment = TextAlignmentOptions.Center;
            messageText.fontSize = 42;
            messageText.color = Color.yellow;
            messageText.raycastTarget = false;
            messageText.rectTransform.sizeDelta = new Vector2(1200f, 120f);
        }
        messageText.text = message;
        messageText.transform.SetAsLastSibling();
        messageText.gameObject.SetActive(true);
        if (messageRoutine != null)
            StopCoroutine(messageRoutine);
        messageRoutine = StartCoroutine(HideMessageAfter(seconds));
    }

    private IEnumerator HideMessageAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        messageText.gameObject.SetActive(false);
    }

    // --- CEZA YAZISI: cezayı yiyenin isimliğinin üstünde "+250 / sebep", büyüyerek çıkar, aşağı düşerek söner ---
    private readonly int[] activePenaltyFloats = new int[5];

    public void ShowPenaltyFloat(int playerQue, int amount, string reason)
    {
        int myQue = 0;
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object val))
            myQue = (int)val;
        if (myQue == 0 || playerQue < 1 || playerQue > 4)
            return;
        Sfx.Play(Sfx.PenaltyFor(playerQue, myQue));
        // turnIndicators sıraya göre dizili (UpdateTurnIndicators ile aynı formül); isimlik onun ebeveyni.
        int idx = (playerQue - myQue + 4) % 4;
        if (idx >= turnIndicators.Length || turnIndicators[idx] == null)
            return;
        Transform nameplate = turnIndicators[idx].transform.parent;
        Canvas canvas = nameplate.GetComponentInParent<Canvas>()?.rootCanvas;
        if (canvas == null)
            return;

        var t = new GameObject("PenaltyFloat", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(canvas.transform, false);
        if (tableLimitText != null)
            t.font = tableLimitText.font;
        t.text = $"+{amount}\n<size=55%>{reason}</size>";
        t.alignment = TextAlignmentOptions.Center;
        t.fontSize = 48;
        t.color = new Color(1f, 0.25f, 0.2f);
        t.outlineWidth = 0.2f;
        t.outlineColor = Color.black;
        t.raycastTarget = false;
        t.rectTransform.sizeDelta = new Vector2(420f, 130f);
        t.rectTransform.position = nameplate.position;
        // Aynı oyuncuya üst üste ceza gelirse (birkaç işleme) yazılar çakışmasın.
        t.rectTransform.anchoredPosition += new Vector2(0f, 90f + 55f * activePenaltyFloats[playerQue]);
        t.transform.SetAsLastSibling();
        activePenaltyFloats[playerQue]++;
        StartCoroutine(PenaltyFloat(t, playerQue));
    }

    private IEnumerator PenaltyFloat(TextMeshProUGUI t, int playerQue)
    {
        RectTransform rt = t.rectTransform;
        Vector2 start = rt.anchoredPosition;
        for (float e = 0f; e < 1.8f && t != null; e += Time.deltaTime)
        {
            rt.localScale =
                Vector3.one
                * (
                    e < 0.2f ? Mathf.Lerp(0.4f, 1.15f, e / 0.2f)
                    : e < 0.3f ? Mathf.Lerp(1.15f, 1f, (e - 0.2f) / 0.1f)
                    : 1f
                );
            float fall = Mathf.Clamp01((e - 0.8f) / 1f);
            rt.anchoredPosition = start + Vector2.down * 70f * fall * fall;
            t.alpha = 1f - fall;
            yield return null;
        }
        if (t != null)
            Destroy(t.gameObject);
        activePenaltyFloats[playerQue]--;
    }
}
