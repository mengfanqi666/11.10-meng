using UnityEngine;
using UnityEngine.UI;

public class BvhDemoUI : MonoBehaviour
{
    public BvhPlayer player;

    public Button playPauseButton;
    public Text playPauseText;
    public Slider speedSlider;
    public Text speedText;
    public Text statusText;

    void Start()
    {
        if (player == null) player = FindObjectOfType<BvhPlayer>();

        if (playPauseButton != null)
            playPauseButton.onClick.AddListener(OnPlayPause);

        if (speedSlider != null)
        {
            speedSlider.minValue = 0.1f;
            speedSlider.maxValue = 3f;
            speedSlider.value = player != null ? player.Speed : 1f;
            speedSlider.onValueChanged.AddListener(OnSpeedChanged);
        }

        UpdateSpeedLabel();
    }

    void OnPlayPause()
    {
        if (player != null) player.TogglePlay();
    }

    void OnSpeedChanged(float v)
    {
        if (player != null) player.SetSpeed(v);
        UpdateSpeedLabel();
    }

    void UpdateSpeedLabel()
    {
        if (speedText != null)
        {
            float s = player != null ? player.Speed : 1f;
            speedText.text = "Speed: " + s.ToString("F1") + "x";
        }
    }

    void Update()
    {
        if (player == null) return;

        if (playPauseText != null)
            playPauseText.text = player.IsPlaying ? "Pause" : "Play";

        if (statusText != null)
        {
            statusText.text = player.Status +
                "\nTime: " + player.CurrentTime.ToString("F2") +
                " / " + player.Duration.ToString("F2") + " s" +
                "\nFrame: " + Mathf.FloorToInt(player.CurrentFrame) +
                " / " + Mathf.Max(0, player.FrameCount - 1);
        }
    }
}