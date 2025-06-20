/* =========================================================================
 *  SongPlayer.cs  – plays a playlist of MIDI files through a given Synth
 * ========================================================================= */
using FluidSynth;
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace FluidMidi
{
    public sealed class SongPlayer : MonoBehaviour
    {
        /* ──────────────────────────────────────────
         *  Static helper controls
         *──────────────────────────────────────────*/
        static readonly ISet<SongPlayer> players = new HashSet<SongPlayer>();
        public static void PauseAll()  { foreach (var p in players) p.Pause();  }
        public static void ResumeAll() { foreach (var p in players) p.Resume(); }
        public static void StopAll()   { foreach (var p in players) p.Stop();   }

        /* ──────────────────────────────────────────
         *  Prepare-job definition
         *──────────────────────────────────────────*/
        struct PrepareJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] [ReadOnly] readonly IntPtr playerPtr;
            public PrepareJob(IntPtr p) => playerPtr = p;
            public void Execute() => Api.Player.Prepare(playerPtr);
        }

        /* ──────────────────────────────────────────
         *  Serialized fields
         *──────────────────────────────────────────*/
        [Header("Playlist")]
        [SerializeField] List<StreamingAsset> playlist = new();
        [SerializeField] bool randomOrder = true;
        [SerializeField] bool autoAdvance = true;

        [Header("Playback")]
        [SerializeField] bool   playOnStart = true;
        [SerializeField] ToggleInt unloadOnStop = new ToggleInt(true, 3);
        [SerializeField] ToggleInt loop         = new ToggleInt(false, 0);
        [SerializeField] ToggleInt endTicks     = new ToggleInt(false, 0);

        [SerializeField, Range(Api.Synth.GAIN_MIN, Api.Synth.GAIN_MAX)]
        float gain  = 0.2f;
        [SerializeField, Range((float)Api.Player.TEMPO_MIN, (float)Api.Player.TEMPO_MAX)]
        float tempo = 1f;
        [SerializeField, BitField] int channels = -1;

        [Header("Skip Control")]
        [SerializeField] bool    skipWithSpace = true;
        [SerializeField] KeyCode skipKey       = KeyCode.Space;

        /* ──────────────────────────────────────────
         *  Runtime state
         *──────────────────────────────────────────*/
        IntPtr    synthPtr;
        IntPtr    playerPtr;
        IntPtr    driver;
        JobHandle prepareJob;

        int   currentIndex = -1;
        float unloadDelay  = -1;
        bool  isInit;
        bool  isPaused;

        Synthesizer synthesizer;

        /* ──────────────────────────────────────────
         *  Public properties
         *──────────────────────────────────────────*/
        public bool IsPlaying => isPaused ||
                                 (playerPtr != IntPtr.Zero &&
                                  Api.Player.GetStatus(playerPtr) == Api.Player.Status.Playing);
        public bool IsPaused   => isPaused;
        public bool IsDone     => unloadDelay >= 0 && !IsPlaying;
        public bool IsReady    => driver != IntPtr.Zero;
        public int  Ticks      => playerPtr != IntPtr.Zero ? Api.Player.GetCurrentTick(playerPtr) : 0;

        /* ──────────────────────────────────────────
         *  Manager-facing API
         *──────────────────────────────────────────*/
        public void Initialize(Synthesizer synth)
        {
            if (isInit) return;

            synthesizer = synth;

            Logger.AddReference();
            Settings.AddReference();
            /* Removed: synthesizer.AddReference();  ← handled by MusicManager */

            synthPtr = Api.Synth.Create(Settings.Ptr);
            Api.Synth.SetGain(synthPtr, gain);

            isInit = true;
            if (playOnStart && playlist.Count > 0)
                PrepareAndPlayNext();
        }

        public void Shutdown()
        {
            if (!isInit) return;
            Cleanup();
            isInit = false;
        }

        /* ──────────────────────────────────────────
         *  Playlist helpers
         *──────────────────────────────────────────*/
        public void ClearPlaylist()                     => playlist.Clear();
        public void AddToPlaylist(StreamingAsset sa)
{
    if (sa != null)
        playlist.Add(sa);
}

        public void StartPlaylist()
{
            if (!isInit || playlist.Count == 0) return;
            PrepareAndPlayNext();      // loads next file and calls Play() internally
        }
        /* ──────────────────────────────────────────
         *  Basic playback controls
         *──────────────────────────────────────────*/
        public void Play()
        {
            Debug.Log("1 play11!!");
            if (!isInit || playerPtr == IntPtr.Zero) return;
            if (!IsPlaying)
            {
                Api.Player.Seek(playerPtr, 0);
                Api.Player.Play(playerPtr);
                unloadDelay = unloadOnStop.Value;
                isPaused = false;
                Debug.Log("play!!");
            }
        }

        public void Stop()
        {
            if (IsPlaying)
            {
                Api.Player.Stop(playerPtr);
                isPaused = false;
            }
        }

        public void Pause()
        {
            if (IsPlaying)
            {
                Api.Player.Stop(playerPtr);
                isPaused = true;
            }
        }

        public void Resume()
        {
            if (isPaused)
            {
                Api.Player.Play(playerPtr);
                isPaused = false;
            }
        }

        /* ──────────────────────────────────────────
         *  Unity messages
         *──────────────────────────────────────────*/
        void Awake()  => players.Add(this);
        void OnDestroy()
        {
            players.Remove(this);
            if (isInit) Cleanup();
        }

        void Update()
        {
            if (!isInit) return;

            /* Skip key -------------------------------------------------- */
            if (skipWithSpace && Input.GetKeyDown(skipKey))
            {
                PrepareAndPlayNext();
                return;
            }

            /* Auto-advance at end -------------------------------------- */
            if (autoAdvance && playerPtr != IntPtr.Zero &&
                Api.Player.GetStatus(playerPtr) == Api.Player.Status.Done)
            {
                PrepareAndPlayNext();
                return;
            }

            /* Create driver once prepared ------------------------------ */
            if (driver == IntPtr.Zero && prepareJob.IsCompleted &&
                synthesizer.SoundFontPtr != IntPtr.Zero)
                CreateDriver();

            /* Auto-unload countdown ------------------------------------ */
            if (unloadOnStop.Enabled && IsDone)
            {
                unloadDelay -= Time.unscaledDeltaTime;
                if (unloadDelay <= 0) unloadDelay = 0;
            }
        }

        /* ──────────────────────────────────────────
         *  Internal helpers
         *──────────────────────────────────────────*/
        void PrepareAndPlayNext()
        {
            if (playlist.Count == 0) return;

            int next = randomOrder
                       ? UnityEngine.Random.Range(0, playlist.Count)
                       : (currentIndex + 1) % playlist.Count;

            if (randomOrder && playlist.Count > 1)
                while (next == currentIndex)
                    next = UnityEngine.Random.Range(0, playlist.Count);

            currentIndex = next;

            LoadSong(playlist[currentIndex]);
            Play();
        }

        void LoadSong(StreamingAsset asset)
        {
         if (asset == null || string.IsNullOrEmpty(asset.GetFullPath()))
{
    Logger.LogError("Missing StreamingAsset in playlist");
    return;
}

            prepareJob.Complete();
            if (playerPtr != IntPtr.Zero) Api.Player.Destroy(playerPtr);

            playerPtr = Api.Player.Create(synthPtr);
            Api.Player.SetTempo(playerPtr, Api.Player.TempoType.Internal, tempo);
            Api.Player.SetActiveChannels(playerPtr, channels);
            Api.Player.Add(playerPtr, asset.GetFullPath());

            if (loop.Enabled)
            {
                Api.Player.SetLoop(playerPtr, -1);
                if (loop.Value > 0) Api.Player.SetLoopBegin(playerPtr, loop.Value);
            }

            Api.Player.SetEnd(playerPtr, endTicks.Enabled ? endTicks.Value : -1);
            Api.Player.Stop(playerPtr);

            prepareJob  = new PrepareJob(playerPtr).Schedule();
            unloadDelay = -1;
        }

        void CreateDriver()
        {
            if (driver != IntPtr.Zero || !prepareJob.IsCompleted) return;
            if (Api.Synth.SoundFontCount(synthPtr) == 0 && synthesizer.SoundFontPtr != IntPtr.Zero)
                Api.Synth.AddSoundFont(synthPtr, synthesizer.SoundFontPtr);

            driver = Api.Driver.Create(Settings.Ptr, synthPtr);
        }

        void Cleanup()
        {
            if (driver != IntPtr.Zero) Api.Driver.Destroy(driver);
            else if (!prepareJob.IsCompleted) Logger.LogWarning("SongPlayer cleaned up before prepare done");

            prepareJob.Complete();
            if (playerPtr != IntPtr.Zero) Api.Player.Destroy(playerPtr);
            playerPtr = IntPtr.Zero;

            Api.Synth.Destroy(synthPtr);
            synthPtr = IntPtr.Zero;

            /* Removed: synthesizer.RemoveReference();  ← handled by MusicManager */
            Settings.RemoveReference();
            Logger.RemoveReference();
        }

        bool ValidateChannel(int ch)
        {
            if (ch < 1 || ch > 16) { Logger.LogError($"Invalid channel: {ch}"); return false; }
            return true;
        }
    }
}
