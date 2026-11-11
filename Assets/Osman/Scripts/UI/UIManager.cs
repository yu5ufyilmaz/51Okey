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

    [Header("Game Over Screen References")]
    public GameObject gameOverPanel; // Sahnedeki ana panel
    public List<PlayerResultSlot> playerResultSlots; // 4 adet sütunu buraya sürükleyeceksin

    [Header("Texts - Game Info")]
    public TextMeshProUGUI tileCountText;
    public TextMeshProUGUI tableLimitText;

    [Header("Texts - Player Stats")]
    public TextMeshProUGUI currentScoreText;
    public TextMeshProUGUI currentPairText;

    [Header("Texts - Game Over")]
    public TextMeshProUGUI rankingText;
    public TextMeshProUGUI restartTimerText;

    [Header("Visuals")]
    public GameObject[] turnIndicators; // 0:Me, 1:Right, 2:Top, 3:Left

    // --- [YENİ] CANLI SKOR TABLOSU ---
    [Header("Live Scoreboard UI")]
    public GameObject liveScorePanel; // Açılıp kapanacak panel
    public TextMeshProUGUI liveScoreContentText; // Puanların yazacağı text
    private bool isScoreboardOpen = false;

    [System.Serializable]
    public class PlayerResultSlot
    {
        public int seatNumber; // 1, 2, 3, 4 (Hangi koltuk olduğu)
        public TMPro.TextMeshProUGUI playerNameText;
        public TMPro.TextMeshProUGUI rewardText; // Düşerler (Üst)
        public TMPro.TextMeshProUGUI penaltyText; // Cezalar (Orta)
        public TMPro.TextMeshProUGUI netScoreText; // Toplam (Alt)
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        if (gameOverPanel)
            gameOverPanel.SetActive(false);
    }

    // UIManager.cs içine ekle

    private void Start()
    {
        // Oyun başlar başlamaz "Anlık Puan" tablosundaki eski yazıları sil.
        // ScoreManager'dan puanlar 0'lanıp gelene kadar boş dursun veya temiz gözüksün.
        if (liveScoreContentText != null)
        {
            liveScoreContentText.text = "";
        }

        // Eğer oyun başında panel açıksa (ki genelde kapalı başlar ama) temiz bir liste oluştur.
        if (isScoreboardOpen)
        {
            UpdateLiveScoreboardText();
        }
    }

    // --- GÜNCELLEME METODLARI ---
    public void ToggleLiveScoreboard()
    {
        if (liveScorePanel == null)
            return;

        isScoreboardOpen = !isScoreboardOpen;
        liveScorePanel.SetActive(isScoreboardOpen);

        if (isScoreboardOpen)
        {
            UpdateLiveScoreboardText();
        }
    }

    public void UpdateLiveScoreboardText()
    {
        if (liveScoreContentText == null)
            return;

        string content = "<size=110%><b>ANLIK CEZA PUANLARI</b></size>\n\n";

        foreach (var player in PhotonNetwork.PlayerList)
        {
            string pName = player.NickName;
            int score = 0;

            // Puanı Photon'dan çek
            if (player.CustomProperties.TryGetValue("PlayerScore", out object val))
            {
                score = (int)val;
            }

            string color = player.IsLocal ? "green" : "white";
            content += $"<color={color}>{pName}: {score}</color>\n";
        }

        liveScoreContentText.text = content;
    }

    public override void OnPlayerPropertiesUpdate(
        Player targetPlayer,
        ExitGames.Client.Photon.Hashtable changedProps
    )
    {
        // Eğer değişen özellik "PlayerScore" ise (yani biri ceza yediyse)
        if (changedProps.ContainsKey("PlayerScore"))
        {
            Debug.Log($"Puan Güncellemesi Algılandı: {targetPlayer.NickName}");

            // Eğer tablo açıksa anında yazıyı güncelle
            if (isScoreboardOpen)
            {
                UpdateLiveScoreboardText();
            }
        }
    }

    // Biri ceza yiyince anlık yenilemek için
    public void RefreshLiveScoreboardIfOpen()
    {
        if (isScoreboardOpen)
            UpdateLiveScoreboardText();
    }

    public void UpdateTileCount(int count)
    {
        if (tileCountText)
            tileCountText.text = count.ToString();
    }

    public void UpdatePlayerStats(int myScore, int myPairsScore, int currentLimit)
    {
        // 1. Seri Puanı
        if (currentScoreText != null)
        {
            string colorHex = (myScore > currentLimit) ? "green" : "white";
            currentScoreText.text = $"Seri Puan: <color={colorHex}>{myScore}</color>";
        }

        // 2. Çift Puanı (Dikkat: Buraya artık Puan geliyor)
        if (currentPairText != null)
        {
            string colorHex = (myPairsScore > currentLimit) ? "green" : "white";
            // İstersen burada "Çift Puanı" yazabilirsin
            currentPairText.text = $"Çift Puan: <color={colorHex}>{myPairsScore}</color>";
        }

        // 3. Ortak Baraj
        if (tableLimitText != null)
        {
            tableLimitText.text = $"Baraj: {currentLimit}";
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

    // GameManager'dan gelen 3 ayrı listeyi (Diziyi) alıyoruz
    // GameManager'dan gelen 3 ayrı listeyi (Diziyi) alıyoruz
    public void ShowDetailedGameOver(
        Dictionary<int, int> rewards,
        Dictionary<int, int> penalties,
        Dictionary<int, int> netScores
    )
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        // --- KAZANANI BULMA MANTIĞI (BAŞLANGIÇ) ---
        int lowestScore = int.MaxValue; // En düşük puanı bulmak için yüksek bir sayıdan başlatıyoruz
        string winnerName = "";
        // ------------------------------------------

        // Tüm slotları gez ve verileri doldur
        foreach (var slot in playerResultSlots)
        {
            // O koltukta oturan oyuncuyu bul
            Photon.Realtime.Player p = GetPlayerBySeat(slot.seatNumber);

            if (p != null)
            {
                // 1. İsim
                slot.playerNameText.text = p.NickName;

                // 2. Düşer (Reward)
                if (rewards.ContainsKey(slot.seatNumber))
                {
                    int val = rewards[slot.seatNumber];
                    slot.rewardText.text = val.ToString();
                    slot.rewardText.color = Color.green;
                }
                else
                {
                    slot.rewardText.text = "0";
                }

                // 3. Ceza (Penalty)
                if (penalties.ContainsKey(slot.seatNumber))
                {
                    int val = penalties[slot.seatNumber];
                    slot.penaltyText.text = "+" + val.ToString();
                    slot.penaltyText.color = Color.red;
                }
                else
                {
                    slot.penaltyText.text = "0";
                }

                // 4. Toplam (Net Score)
                if (netScores.ContainsKey(slot.seatNumber))
                {
                    int val = netScores[slot.seatNumber];
                    slot.netScoreText.text = val.ToString();
                    slot.netScoreText.fontStyle = TMPro.FontStyles.Bold;

                    if (val > 0)
                        slot.netScoreText.color = Color.red;
                    else if (val < 0)
                        slot.netScoreText.color = Color.green;
                    else
                        slot.netScoreText.color = Color.white;

                    // --- KAZANAN KONTROLÜ ---
                    // Eğer bu oyuncunun puanı şu ana kadar bulduğumuz en düşük puandan daha azsa
                    if (val < lowestScore)
                    {
                        lowestScore = val;
                        winnerName = p.NickName;
                    }
                    // ------------------------
                }
                else
                {
                    slot.netScoreText.text = "0";
                }
            }
            else
            {
                // O koltukta oyuncu yoksa boş göster
                slot.playerNameText.text = "-";
                slot.rewardText.text = "";
                slot.penaltyText.text = "";
                slot.netScoreText.text = "";
            }
        }

        // --- KAZANANI YAZDIRMA ---
        if (rankingText != null)
        {
            if (!string.IsNullOrEmpty(winnerName))
            {
                // İstersen rengini altın sarısı vs. yapabilirsin
                rankingText.color = Color.yellow;
                rankingText.text = $"{winnerName} Oyunu Kazandı!";
            }
            else
            {
                rankingText.text = "";
            }
        }
        // -------------------------
    }

    // Yardımcı Fonksiyon: Koltuk Numarasına Göre Oyuncuyu Bulma
    private Photon.Realtime.Player GetPlayerBySeat(int seatNum)
    {
        foreach (var p in Photon.Pun.PhotonNetwork.PlayerList)
        {
            if (p.CustomProperties.TryGetValue("PlayerQue", out object q))
            {
                if ((int)q == seatNum)
                    return p;
            }
        }
        return null;
    }

    private IEnumerator TimerRoutine()
    {
        for (int i = 10; i > 0; i--)
        {
            if (restartTimerText)
                restartTimerText.text = $"Lobiye dönüş: {i}s";
            yield return new WaitForSeconds(1f);
        }
    }
}
