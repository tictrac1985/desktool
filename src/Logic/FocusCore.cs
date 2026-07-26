using System;

namespace DeskMate.Logic
{
    /// <summary>Focus (pomodoro) timer state machine. Pure: the caller injects the clock
    /// and decides what happens when a phase completes (sound, counter, next phase).</summary>
    public class FocusCore
    {
        public enum Phase { Idle, Work, Break }

        public Phase State { get; private set; }
        public bool Paused { get; private set; }
        public int WorkMin = 25, BreakMin = 5;

        DateTime _endsAt;
        TimeSpan _total = TimeSpan.Zero;
        TimeSpan _frozen = TimeSpan.Zero;
        bool _completionFired;

        public FocusCore() { State = Phase.Idle; }

        public void Start(Phase phase, DateTime now)
        {
            State = phase;
            Paused = false;
            _completionFired = false;
            _total = TimeSpan.FromMinutes(phase == Phase.Work ? WorkMin : BreakMin);
            _endsAt = now + _total;
        }

        public void TogglePause(DateTime now)
        {
            if (State == Phase.Idle) return;
            if (Paused)
            {
                _endsAt = now + _frozen;
                Paused = false;
            }
            else
            {
                _frozen = _endsAt - now;
                if (_frozen < TimeSpan.Zero) _frozen = TimeSpan.Zero;
                Paused = true;
            }
        }

        public void Reset()
        {
            State = Phase.Idle;
            Paused = false;
            _total = _frozen = TimeSpan.Zero;
            _completionFired = false;
        }

        public TimeSpan Remaining(DateTime now)
        {
            if (State == Phase.Idle) return TimeSpan.Zero;
            if (Paused) return _frozen;
            var left = _endsAt - now;
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }

        /// <summary>0..1 progress of the running phase (0 when idle).</summary>
        public double Progress(DateTime now)
        {
            if (State == Phase.Idle || _total <= TimeSpan.Zero) return 0;
            double done = (_total - Remaining(now)).TotalSeconds / _total.TotalSeconds;
            return done < 0 ? 0 : done > 1 ? 1 : done;
        }

        /// <summary>Returns true exactly once when the running phase reaches zero.</summary>
        public bool Tick(DateTime now)
        {
            if (State == Phase.Idle || Paused || _completionFired) return false;
            if (now < _endsAt) return false;
            _completionFired = true;
            return true;
        }

        public static string Format(TimeSpan span)
        {
            int totalSec = (int)Math.Ceiling(span.TotalSeconds);
            if (totalSec < 0) totalSec = 0;
            return string.Format("{0:00}:{1:00}", totalSec / 60, totalSec % 60);
        }
    }
}
