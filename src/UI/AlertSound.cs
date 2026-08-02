using System;
using System.IO;
using System.Media;
using DeskMate.Core;
using DeskMate.Logic;

namespace DeskMate.UI
{
    /// <summary>Plays the user's chosen alert sound: system / generated soft chime / custom wav.</summary>
    public static class AlertSound
    {
        static byte[] _chime;

        /// <summary>SoundPlayer.Play() is asynchronous: it hands a buffer to the audio stack and
        /// returns. Disposing the player — or letting the GC collect it — while the sound is
        /// still playing truncates or garbles it. Holding the current player in a static field
        /// keeps it (and its buffer) reachable until the next alert replaces it.</summary>
        static SoundPlayer _playing;
        static readonly object Gate = new object();

        public static void Play()
        {
            try
            {
                switch (Settings.I.AdhanSound)
                {
                    case 1: PlayChime(); break;
                    case 2:
                        if (!string.IsNullOrEmpty(Settings.I.AdhanPath) && File.Exists(Settings.I.AdhanPath))
                            Start(new SoundPlayer(Settings.I.AdhanPath));
                        else PlayChime(); // custom chosen but file missing — still audible
                        break;
                    default: SystemSounds.Exclamation.Play(); break;
                }
            }
            catch (Exception ex) { Log.Error("sound", ex); }
        }

        public static void PlayChime()
        {
            try
            {
                if (_chime == null) _chime = ChimeWave.GenerateSoftChime();
                Start(new SoundPlayer(new MemoryStream(_chime)));
            }
            catch (Exception ex) { Log.Error("chime", ex); }
        }

        static void Start(SoundPlayer player)
        {
            SoundPlayer previous;
            lock (Gate)
            {
                previous = _playing;
                _playing = player;
            }
            if (previous != null) { try { previous.Dispose(); } catch { } }
            player.Play();
        }
    }
}
