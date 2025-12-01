using System.Collections.Generic;

// Bu script oyun içindeki olaylarda taşınacak veri paketlerini (DTO) tutar.
// MonoBehaviour'dan miras ALMAZ.

[System.Serializable]
public class HandData
{
    public int actorNumber; // Oyuncunun ID'si
    public bool isFinishMove; // Bu hamle oyunu bitiren hamle mi?
    public List<Tiles> handTiles; // O an elindeki taşlar (Bitip bitmediğini kontrol için)
}
