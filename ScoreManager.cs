public static class ScoreManager
{
    public static int Score { get; private set; }

    public static void SetScore(int score)
    {
        Score = score;
    }

    public static void Reset()
    {
        Score = 0;
    }
}

