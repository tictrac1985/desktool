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

        public static void Play()
        {
            try
            {
                switch (Settings.I.AdhanSound)
                {
                    case 1: PlayChime(); break;
                    case 2:
                        if (!string.IsNullOrEmpty(Settings.I.AdhanPath) && File.Exists(Settings.I.AdhanPath))
                        {
                            using (var p = new SoundPlayer(Settings.I.AdhanPath)) p.Play();
                        }
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
                var p = new SoundPlayer(new MemoryStream(_chime));
                p.Play();
                // keep the player alive until playback ends
                p.Disposed += (s, e) => { };
            }
            catch (Exception ex) { Log.Error("chime", ex); }
        }
    }
}
