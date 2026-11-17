using System;
using System.Runtime.InteropServices;

namespace DeskMate.Core
{
    /// <summary>Master volume and mute of the default speakers and the default microphone,
    /// through the Windows Core Audio endpoint API (Vista and later). Three small COM
    /// interfaces declared by hand — no interop assembly, no extra reference. Every call
    /// answers false instead of throwing: a machine with no sound card, or with the audio
    /// service stopped, simply shows the control as unavailable.</summary>
    public static class Audio
    {
        const int ERender = 0, ECapture = 1;
        const int ERoleMultimedia = 1, ERoleCommunications = 2;
        const int ClsCtxAll = 23;

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        class MMDeviceEnumerator { }

        // Only the members actually called are spelled out; the placeholders before them
        // keep each method at its real slot in the COM vtable.
        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
                                       [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        }

        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioEndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int GetChannelCount(out uint count);
            [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid context);
            [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
            [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
            [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid context);
            [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
            [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDb);
            [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        }

        /// <summary>Runs <paramref name="use"/> against the default endpoint's volume control
        /// and releases every COM object afterwards. False when there is no such device.</summary>
        static bool With(bool microphone, Func<IAudioEndpointVolume, bool> use)
        {
            object enumObj = null, volObj = null;
            IMMDevice device = null;
            try
            {
                enumObj = new MMDeviceEnumerator();
                var en = (IMMDeviceEnumerator)enumObj;
                int hr = en.GetDefaultAudioEndpoint(microphone ? ECapture : ERender,
                    microphone ? ERoleCommunications : ERoleMultimedia, out device);
                if (hr != 0 || device == null) return false;
                Guid iid = typeof(IAudioEndpointVolume).GUID;
                hr = device.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out volObj);
                if (hr != 0 || volObj == null) return false;
                return use((IAudioEndpointVolume)volObj);
            }
            catch (Exception ex)
            {
                Log.Error("audio", ex);
                return false;
            }
            finally
            {
                Release(volObj); Release(device); Release(enumObj);
            }
        }

        static void Release(object o)
        {
            try { if (o != null && Marshal.IsComObject(o)) Marshal.ReleaseComObject(o); } catch { }
        }

        /// <summary>Level 0..1 and mute state of the speakers (or the microphone).</summary>
        public static bool TryGet(bool microphone, out double level, out bool muted)
        {
            double lv = 0; bool mu = false;
            bool ok = With(microphone, v =>
            {
                float f; bool m;
                if (v.GetMasterVolumeLevelScalar(out f) != 0) return false;
                if (v.GetMute(out m) != 0) return false;
                lv = f; mu = m;
                return true;
            });
            level = Math.Max(0, Math.Min(1, lv));
            muted = mu;
            return ok;
        }

        /// <summary>Sets the speaker level (0..1). Raising the level from zero also unmutes —
        /// dragging a slider up and hearing nothing is never what was meant.</summary>
        public static bool SetLevel(double level)
        {
            float f = (float)Math.Max(0, Math.Min(1, level));
            return With(false, v =>
            {
                Guid ctx = Guid.Empty;
                if (v.SetMasterVolumeLevelScalar(f, ref ctx) != 0) return false;
                bool m;
                if (f > 0.005f && v.GetMute(out m) == 0 && m) v.SetMute(false, ref ctx);
                return true;
            });
        }

        public static bool SetMute(bool microphone, bool mute)
        {
            return With(microphone, v =>
            {
                Guid ctx = Guid.Empty;
                return v.SetMute(mute, ref ctx) == 0;
            });
        }
    }
}
