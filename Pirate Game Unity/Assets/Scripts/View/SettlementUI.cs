using UnityEngine;
using UnityEngine.UI;

namespace PirateGame.View
{
    public class ResultsScreen : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] Text goldText;
        [SerializeField] Text scoreText;
        [SerializeField] Text killsText;
        [SerializeField] Button continueButton;

        public void Show(ulong gold, ulong contributionScore, uint kills)
        {
            panel.SetActive(true);
            if (goldText) goldText.text = $"Gold: {gold}";
            if (scoreText) scoreText.text = $"Contribution: {contributionScore}";
            if (killsText) killsText.text = $"Kills: {kills}";
        }

        public void Hide() => panel.SetActive(false);
    }

    public class SettlementController : MonoBehaviour
    {
        [SerializeField] Net.BlockchainConfig config;
        [SerializeField] ResultsScreen resultsScreen;

        public void OnMatchEnded(ulong gold, ulong contributionScore, uint kills)
        {
            // end_match on ER -> commit_and_undelegate
            // credit_profile on base layer
            Debug.Log($"[Settlement] gold={gold} score={contributionScore} kills={kills}");
            resultsScreen?.Show(gold, contributionScore, kills);
        }
    }
}
