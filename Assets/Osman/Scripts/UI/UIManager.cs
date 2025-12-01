using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using TMPro; // <-- TextMeshPro kütüphanesi eklendi
using UnityEngine;
using UnityEngine.UI; // Panel vs. için

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Game Over UI")]
    public GameObject gameOverPanel;

    // Değişiklik Burada: Text yerine TextMeshProUGUI kullanıyoruz
    public TextMeshProUGUI rankingText;
    public TextMeshProUGUI restartTimerText;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    // UIManager.cs -> ShowGameOver (GÜNCELLEME)

    public void ShowGameOver(Dictionary<int, int> playerScores)
    {
        if (gameOverPanel == null)
            return;
        gameOverPanel.SetActive(true);

        // Puanı KÜÇÜK olan daha iyidir (Ceza puanı mantığı)
        var sortedScores = playerScores.OrderBy(x => x.Value).ToList();

        string rankingString = "<size=120%>--- SKOR TABLOSU ---</size>\n\n";
        int rank = 1;

        foreach (var item in sortedScores)
        {
            string pName = "Oyuncu " + item.Key; // Varsayılan isim

            // İsmi bulmaya çalış
            foreach (var p in PhotonNetwork.PlayerList)
            {
                if (p.CustomProperties.TryGetValue("PlayerQue", out object q) && (int)q == item.Key)
                {
                    pName = p.NickName;
                    break;
                }
            }

            // Renkli Yazdırma
            if (rank == 1)
                rankingString += $"<color=yellow>{rank}. {pName} : {item.Value} Puan</color>\n";
            else
                rankingString += $"{rank}. {pName} : {item.Value} Puan\n";

            rank++;
        }

        if (rankingText != null)
            rankingText.text = rankingString;

        // Sayaç yeniden başlamasın diye kontrol
        StopAllCoroutines();
        StartCoroutine(UpdateRestartTimer());
    }

    private IEnumerator UpdateRestartTimer()
    {
        int timeLeft = 10;
        while (timeLeft > 0)
        {
            if (restartTimerText != null)
                restartTimerText.text =
                    $"Oyun <color=red>{timeLeft}</color> saniye sonra yeniden başlayacak...";

            yield return new WaitForSeconds(1f);
            timeLeft--;
        }
    }
}
