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

        // Tüm slotları gez ve verileri doldur
        foreach (var slot in playerResultSlots)
        {
            // O koltukta oturan oyuncuyu bul
            Photon.Realtime.Player p = GetPlayerBySeat(slot.seatNumber);

            if (p != null)
            {
                // 1. İsim
                slot.playerNameText.text = p.NickName;

                // 2. Düşer (Reward) - Eksi Puanlar
                if (rewards.ContainsKey(slot.seatNumber))
                {
                    int val = rewards[slot.seatNumber];
                    slot.rewardText.text = val.ToString();
                    // İsteğe bağlı renk: Yeşil
                    slot.rewardText.color = Color.green;
                }
                else
                {
                    slot.rewardText.text = "0";
                }

                // 3. Ceza (Penalty) - Artı Puanlar
                if (penalties.ContainsKey(slot.seatNumber))
                {
                    int val = penalties[slot.seatNumber];
                    slot.penaltyText.text = "+" + val.ToString(); // Önüne artı koyduk
                    // İsteğe bağlı renk: Kırmızı
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
                    slot.netScoreText.fontStyle = TMPro.FontStyles.Bold; // Kalın yap

                    // Pozitifse Kırmızı, Negatifse Yeşil yapabilirsin (İsteğe bağlı)
                    if (val > 0)
                        slot.netScoreText.color = Color.red;
                    else if (val < 0)
                        slot.netScoreText.color = Color.green;
                    else
                        slot.netScoreText.color = Color.white;
                }
                else
                {
                    slot.netScoreText.text = "0";
                }
            }
            else
            {
                // O koltukta oyuncu yoksa boş göster veya gizle
                slot.playerNameText.text = "-";
                slot.rewardText.text = "";
                slot.penaltyText.text = "";
                slot.netScoreText.text = "";
            }
        }
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
