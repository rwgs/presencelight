using System;

namespace PresenceLight.Core.PresenceServices
{
    /// <summary>
    /// What the light should do about the most recent attempt to read presence.
    /// </summary>
    public enum PresenceFreshness
    {
        /// <summary>
        /// The most recent read succeeded. Show the presence it returned.
        /// </summary>
        Fresh,

        /// <summary>
        /// A read failed, but a successful one is recent enough to keep showing.
        /// Hold the current colour rather than reacting to a single failure.
        /// </summary>
        Holding,

        /// <summary>
        /// No read has succeeded within the configured window. The presence on
        /// display can no longer be trusted, so the light must say so instead.
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Decides whether presence on display is still trustworthy.
    /// </summary>
    /// <remarks>
    /// A failed or stale read must never keep a light showing that someone is
    /// available, and a light left on its last colour cannot be told apart from a
    /// correct one. This tracks when presence was last read successfully so a
    /// stale reading can be replaced with an explicit unknown indication, while
    /// tolerating the isolated failures that a poll every few seconds will produce.
    /// </remarks>
    public class PresenceFreshnessTracker
    {
        private DateTime? _lastSuccessAt;
        private DateTime? _startedAt;

        /// <param name="unknownAfter">
        /// How long presence may go unconfirmed before the light shows unknown. A
        /// value of zero or less means a single failed read is enough.
        /// </param>
        public PresenceFreshnessTracker(TimeSpan unknownAfter)
        {
            UnknownAfter = unknownAfter;
        }

        /// <summary>
        /// How long presence may go unconfirmed before the light shows unknown. Settable
        /// so that editing the setting takes effect without restarting the application,
        /// which is how the rest of the light settings behave.
        /// </summary>
        public TimeSpan UnknownAfter { get; set; }

        /// <summary>
        /// When presence was last read successfully, or null if it never has been.
        /// </summary>
        public DateTime? LastSuccessAt => _lastSuccessAt;

        /// <summary>
        /// The freshness reported by the most recent call to <see cref="RecordSuccess"/>
        /// or <see cref="RecordFailure"/>.
        /// </summary>
        public PresenceFreshness Current { get; private set; } = PresenceFreshness.Unknown;

        /// <summary>
        /// Whether any read has been recorded yet, successful or not.
        /// </summary>
        /// <remarks>
        /// <see cref="Current"/> starts at <see cref="PresenceFreshness.Unknown"/>, which
        /// makes a brand new tracker indistinguishable from one that has given up on a
        /// real outage. Anything reporting a recovery needs to tell those apart, so that
        /// the first successful read of a healthy start is not announced as a recovery.
        /// </remarks>
        public bool HasAttempted => _startedAt.HasValue;

        /// <summary>
        /// Records a presence read that returned a usable presence.
        /// </summary>
        public PresenceFreshness RecordSuccess(DateTime at)
        {
            _startedAt ??= at;
            _lastSuccessAt = at;
            Current = PresenceFreshness.Fresh;
            return Current;
        }

        /// <summary>
        /// Records a presence read that failed or returned nothing usable.
        /// </summary>
        /// <remarks>
        /// Before the first success the window is measured from the first attempt, so
        /// a process that starts unable to reach Graph reaches unknown on the same
        /// schedule instead of leaving whatever colour the light already had.
        /// </remarks>
        public PresenceFreshness RecordFailure(DateTime at)
        {
            _startedAt ??= at;

            DateTime confirmedAt = _lastSuccessAt ?? _startedAt.Value;
            Current = at - confirmedAt >= UnknownAfter
                ? PresenceFreshness.Unknown
                : PresenceFreshness.Holding;

            return Current;
        }

        /// <summary>
        /// How long presence has been unconfirmed at the given time, measured from the
        /// last success or, before any success, from the first attempt.
        /// </summary>
        public TimeSpan UnconfirmedFor(DateTime at)
        {
            DateTime? confirmedAt = _lastSuccessAt ?? _startedAt;
            return confirmedAt.HasValue ? at - confirmedAt.Value : TimeSpan.Zero;
        }
    }
}
