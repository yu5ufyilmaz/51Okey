using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Panels")]
    public GameObject gameOverPanel;

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

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        if (gameOverPanel)
            gameOverPanel.SetActive(false);
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

    public void ShowGameOver(Dictionary<int, int> playerScores)
    {
        if (gameOverPanel == null)
            return;

        gameOverPanel.SetActive(true);
        gameOverPanel.transform.SetAsLastSibling();

        // Skorları puana göre sırala (Düşük puan (az ceza) en üstte)
        var sortedScores = playerScores.OrderBy(x => x.Value).ToList();

        string rankingString = "<size=120%>--- MAÇ SONUCU ---</size>\n\n";
        int rank = 1;

        foreach (var item in sortedScores)
        {
            // item.Key = PlayerQue (Sıra Numarası)
            // item.Value = Puan

            string displayName = $"Oyuncu {item.Key}"; // Varsayılan isim (Bulamazsa bunu kullanır)

            // --- İSİM BULMA (GÜÇLENDİRİLMİŞ) ---
            foreach (var p in PhotonNetwork.PlayerList)
            {
                // Custom Properties güvenli okuma
                if (p.CustomProperties.ContainsKey("PlayerQue"))
                {
                    // object türünü güvenli bir şekilde int'e çeviriyoruz
                    int pQue = System.Convert.ToInt32(p.CustomProperties["PlayerQue"]);

                    if (pQue == item.Key)
                    {
                        // Eğer NickName boşsa (nadir olur), ID yazsın
                        displayName = string.IsNullOrEmpty(p.NickName)
                            ? $"User {p.ActorNumber}"
                            : p.NickName;
                        break;
                    }
                }
            }
            // -----------------------------------

            // Renklendirme: 1. olan Altın Sarısı
            if (rank == 1)
                rankingString +=
                    $"<color=yellow>{rank}. {displayName} : {item.Value} Puan (KAZANAN)</color>\n";
            else
                rankingString += $"{rank}. {displayName} : {item.Value} Puan\n";

            rank++;
        }

        if (rankingText != null)
            rankingText.text = rankingString;

        StopAllCoroutines();
        StartCoroutine(TimerRoutine());
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
