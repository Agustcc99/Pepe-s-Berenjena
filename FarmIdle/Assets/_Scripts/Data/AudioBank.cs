using System;
using UnityEngine;

namespace FarmIdle
{
    // Cada sonido que el juego sabe reproducir. El AudioManager los dispara escuchando eventos de Managers; el gameplay y la UI no
    // conocen el audio.
    public enum SoundCue
    {
        Harvest,     // un producto entra a la mochila (sube de nota si se cosecha seguido)
        Sell,        // se vende un producto (sube el tono si se venden varios seguidos)
        Purchase,    // se abre una zona o sube una mejora
        LevelUp,     // sube de nivel
        Perk,        // se elige un perk
        Fertilize,   // se usa el fertilizante
        Offer,       // aparece el símbolo de oferta
        Reward,      // se gana la recompensa de un video
        Denied,      // "no se puede": sin monedas, mochila llena, sin anuncio...
        Turbo,       // arranca el Turbo
        Step,        // se cumple un paso de la guía de los primeros minutos
        Click,       // tocar un botón de la interfaz
        // Los nuevos van SIEMPRE al final: el banco guarda el índice.
        BackpackFull,   // la mochila se llenó con esta cosecha (festejo, no error)
        TruckHorn       // terminó una venta grande: bocina del camión
    }

    // Un sonido: sus variantes (se sortea una distinta cada vez), volumen, rango de tono y el mínimo entre dos disparos.
    [Serializable]
    public sealed class SoundEntry
    {
        [SerializeField] private SoundCue cue;
        [SerializeField, Tooltip("Variantes del sonido: se elige una al azar, sin repetir la anterior.")]
        private AudioClip[] clips = new AudioClip[0];
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField, Range(0.5f, 2f)] private float pitchMin = 0.97f;
        [SerializeField, Range(0.5f, 2f)] private float pitchMax = 1.05f;
        [SerializeField, Min(0f), Tooltip("Segundos (sin escala) mínimos entre dos disparos de este sonido: evita ametralladoras.")]
        private float minIntervalSeconds = 0.05f;

        public SoundCue Cue => cue;
        public AudioClip[] Clips => clips;
        public float Volume => volume;
        public float PitchMin => pitchMin;
        public float PitchMax => Mathf.Max(pitchMin, pitchMax);
        public float MinIntervalSeconds => minIntervalSeconds;
    }

    // Todos los sonidos y la música del juego en un solo asset de solo lectura (GameConfig lo referencia). Los efectos son de Kenney
    // (CC0); la música de fondo es un loop generado para el proyecto (ver Assets/Audio/CREDITS.txt).
    [CreateAssetMenu(fileName = "AudioBank", menuName = "FarmIdle/Datos/Banco de audio")]
    public sealed class AudioBank : ScriptableObject
    {
        [SerializeField] private SoundEntry[] sounds = new SoundEntry[0];

        [Header("Música")]
        [SerializeField, Tooltip("Loop corto y suave de fondo. Puede faltar: sin música.")]
        private AudioClip music;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.35f;

        public AudioClip Music => music;
        public float MusicVolume => musicVolume;

        // El sonido de ese cue, o null si el banco no lo trae.
        public SoundEntry Find(SoundCue cue)
        {
            if (sounds == null) return null;

            for (int i = 0; i < sounds.Length; i++)
            {
                if (sounds[i] != null && sounds[i].Cue == cue) return sounds[i];
            }

            return null;
        }
    }
}
