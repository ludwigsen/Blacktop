using UnityEngine;
using UnityEngine.UI;

public class TeamScorePanel : MonoBehaviour
{
    [SerializeField] Text teamName;
    [SerializeField] Text scoreText;
    [SerializeField] Image gamebreakerFill;

    public void SetTeam(string name)
    {
        teamName.text = name;
    }

    public void SetScore(int score)
    {
        scoreText.text = score.ToString();
    }

    public void SetGamebreaker(float value)
    {
        gamebreakerFill.fillAmount = value / 100f;
    }
}