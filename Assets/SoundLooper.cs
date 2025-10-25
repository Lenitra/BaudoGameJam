using UnityEngine;

public class SoundLooper : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;

    [Header("Loop Settings")]
    [Tooltip("Timestamp de retour en format HH:MM:SS:MS (heures:minutes:secondes:millisecondes)")]
    [SerializeField] private string loopBackTimestamp = "00:02:27:10";

    private float loopBackTime; // Temps en secondes pour le retour

    void Start()
    {
        // Si aucun AudioSource n'est assigné, essayer de le récupérer sur le même GameObject
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        // Convertir le timestamp en secondes
        loopBackTime = ConvertTimestampToSeconds(loopBackTimestamp);

        // Démarrer la lecture si pas déjà en cours
        if (audioSource != null && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    void Update()
    {
        // Vérifier si l'AudioSource existe et est en lecture
        if (audioSource != null && audioSource.isPlaying)
        {
            // Détecter si on approche de la fin (avec une petite marge pour éviter les coupes)
            float timeRemaining = audioSource.clip.length - audioSource.time;

            // Quand il reste moins de 0.1 seconde, revenir au timestamp de loop
            if (timeRemaining < 0.1f)
            {
                audioSource.time = loopBackTime;
            }
        }
    }

    /// <summary>
    /// Convertit un timestamp au format HH:MM:SS:MS en secondes
    /// </summary>
    private float ConvertTimestampToSeconds(string timestamp)
    {
        string[] parts = timestamp.Split(':');

        if (parts.Length != 4)
        {
            Debug.LogWarning($"Format de timestamp invalide: {timestamp}. Utilisation de 0 par défaut.");
            return 0f;
        }

        int hours = int.Parse(parts[0]);
        int minutes = int.Parse(parts[1]);
        int seconds = int.Parse(parts[2]);
        int milliseconds = int.Parse(parts[3]);

        // Convertir tout en secondes
        float totalSeconds = (hours * 3600f) + (minutes * 60f) + seconds + (milliseconds / 100f);

        return totalSeconds;
    }
}
