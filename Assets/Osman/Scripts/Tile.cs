[System.Serializable]
public class Tile
{
    public string color; // Taşın rengi (örneğin, "Red", "Black", "Blue", "Yellow")
    public int number;   // Taşın numarası (1-13 veya Joker için 0)

    public Tile(string color, int number)
    {
        this.color = color;
        this.number = number;
    }

    // Joker olup olmadığını kontrol eden bir metot
    public bool IsJoker()
    {
        return this.number == 0;
    }
}