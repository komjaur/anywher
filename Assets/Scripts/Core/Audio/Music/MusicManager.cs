/* =========================================================================
 *  MusicManager.cs  – central controller that ties Synthesizer + SongPlayer
 *  to the game’s current AreaData.
 * ========================================================================= */
using UnityEngine;

namespace FluidMidi
{
    [AddComponentMenu("Fluid MIDI/Music Manager")]
    public sealed class MusicManager : MonoBehaviour
    {
        /* ───── internal refs ───── */
        Synthesizer synthesizer;
        SongPlayer  songPlayer;
        AudioSource audioSource;

        /* ───── bootstrap from GameManager ───── */
        public void Initialize(StreamingAsset soundFont)
        {
            /* parent lives across scenes */
            GameObject parent = new GameObject("music player");
            DontDestroyOnLoad(parent);

            /* 1. AudioSource (FluidSynth driver pushes into this) */
            audioSource             = parent.AddComponent<AudioSource>();
            audioSource.playOnAwake  = false;
            audioSource.spatialBlend = 0f;     // 2-D
            audioSource.loop         = false;

            /* 2. Create Synth & Player – nothing happens yet */
            synthesizer = parent.AddComponent<Synthesizer>();
            songPlayer  = parent.AddComponent<SongPlayer>();

            synthesizer.Initialize(soundFont);
            songPlayer.Initialize(synthesizer);

            /* 3. Subscribe to area-change events */
            var pm = GameManager.Instance.PlayerManager;
            pm.OnAreaChanged += HandleAreaChanged;

            /* trigger once for current area (if already set) */
            if (pm.CurrentArea) HandleAreaChanged(pm.CurrentArea);
        }

   void HandleAreaChanged(AreaData area)
{
    if (area == null || area.MusicPlaylist == null || area.MusicPlaylist.Length == 0)
    {
        songPlayer.Stop();
        return;
    }

    songPlayer.ClearPlaylist();
    foreach (var sa in area.MusicPlaylist)
        songPlayer.AddToPlaylist(sa);

    songPlayer.StartPlaylist();          // <— fixed
}

    }
}
