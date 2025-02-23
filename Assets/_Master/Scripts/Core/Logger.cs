using TMPro;
using UnityEngine;

public class Logger : MonoBehaviour
{
    public static Logger Instance;
    private TextMeshProUGUI m_LogText;

    void Awake()
    {
        Instance = this;
        m_LogText = GetComponent<TextMeshProUGUI>();
    }

    public void Log(string message)
    {
        string logMessage = $"<color=white>[LOG]</color> {message}";
        Debug.Log(logMessage);
        m_LogText.text += $"{logMessage}\n";
    }

    public void LogInfo(string message)
    {
        string logMessage = $"<color=green>[INFO]</color> {message}";
        Debug.Log(logMessage);
        m_LogText.text += $"{logMessage}\n";
    }

    public void LogWarning(string message)
    {
        string logMessage = $"<color=yellow>[WARNING]</color> {message}";
        Debug.LogWarning(logMessage);
        m_LogText.text += $"{logMessage}\n";
    }

    public void LogError(string message)
    {
        string logMessage = $"<color=red>[ERROR]</color> {message}";
        Debug.LogError(logMessage);
        m_LogText.text += $"{logMessage}\n";
    }
}
