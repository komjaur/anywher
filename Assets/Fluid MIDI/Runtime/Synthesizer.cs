/* =========================================================================
 *  Synthesizer.cs  – loads one SoundFont and keeps a reference-counted
 *  FluidSynth synth instance alive for anyone who needs it.
 * ========================================================================= */
using FluidSynth;
using System;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace FluidMidi
{
    public sealed class Synthesizer : MonoBehaviour
    {
        /* ───── inspector (set by MusicManager) ───── */
        [SerializeField] StreamingAsset soundFont = new StreamingAsset();

        /* ───── async job to load the .sf2 ───── */
        struct LoadSoundFontJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] [ReadOnly] readonly IntPtr synth;
            [DeallocateOnJobCompletion] [ReadOnly]          readonly NativeArray<char> path;

            public LoadSoundFontJob(IntPtr synth, string path)
            {
                this.synth = synth;
                path       = path ?? string.Empty;
                this.path  = new NativeArray<char>(path.ToCharArray(), Allocator.Persistent);
            }

            public void Execute()
            {
                string p = new string(path.ToArray());
                if (string.IsNullOrEmpty(p))  { Logger.LogError("No soundfont specified"); return; }
                if (!File.Exists(p))          { Logger.LogError($"Soundfont file missing: {p}"); return; }

                Logger.Log($"Loading soundfont: {p}");
                Api.Synth.LoadSoundFont(synth, p, 0);
            }
        }

        /* ───── internal state ───── */
        int        refCount;
        IntPtr     synthPtr;
        JobHandle  loadJob;
        bool       isInit;

        /* ───── public helpers ───── */
        public bool   IsReady      => isInit && loadJob.IsCompleted;
        public IntPtr SoundFontPtr => IsReady ? Api.Synth.GetSoundFont(synthPtr, 0) : IntPtr.Zero;

        /* ───── called once by MusicManager ───── */
        public void Initialize(StreamingAsset sf)
        {
            if (isInit) return;

            soundFont = sf;
            AddReference();          // kicks off LoadSoundFontJob
            isInit = true;
        }

        public void Shutdown()
        {
            if (!isInit) return;
            RemoveReference();
            isInit = false;
        }

        /* ───── reference management (unchanged logic) ───── */
        void AddReference()
        {
            if (refCount == 0)
            {
                Logger.AddReference();
                Settings.AddReference();

                synthPtr = Api.Synth.Create(Settings.Ptr);
                loadJob  = new LoadSoundFontJob(synthPtr, soundFont.GetFullPath()).Schedule();
            }
            ++refCount;
        }

        void RemoveReference()
        {
            if (--refCount > 0) return;

            if (!loadJob.IsCompleted)
                Logger.LogWarning("Destroying Synthesizer before soundfont finished loading");

            loadJob.Complete();
            Api.Synth.Destroy(synthPtr);
            Settings.RemoveReference();
            Logger.RemoveReference();
        }

        /* ───── validation helpers (unchanged) ───── */
        void OnValidate()
        {
            string p = soundFont.GetFullPath();
            if (p.Length > 0 && Api.Misc.IsSoundFont(p) == 0)
            {
                Logger.LogError($"Not a soundfont: {p}");
                soundFont.SetFullPath(string.Empty);
            }
        }

        void Reset()
        {
            if (!Directory.Exists(Application.streamingAssetsPath)) return;

            string[] sf2 = Directory.GetFiles(Application.streamingAssetsPath, "*.sf2",
                                              SearchOption.AllDirectories);
            if (sf2.Length == 1)
                soundFont.SetFullPath(sf2[0].Replace(Path.DirectorySeparatorChar, '/'));
        }
    }
}
