[System.Serializable]
public class Tile
{
    public string color; // Taşın rengi ("Red", "Black", "Blue", "Yellow")
    public int number;   // Taşın numarası (1-13 veya Joker için 0)

    public Tile(string color, int number)
    {
        this.color = color;
        this.number = number;
    }

    public bool IsJoker()
    {
        return this.number == 0;
    }
}