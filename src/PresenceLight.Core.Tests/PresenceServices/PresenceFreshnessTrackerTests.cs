using PresenceLight.Core.PresenceServices;

namespace PresenceLight.Core.Tests.PresenceServices
{
    /// <summary>
    /// The requirement these cover is that a failed or stale presence must never keep a
    /// light showing that someone is available. A light left on its last colour cannot be
    /// told apart from a correct one, so the tracker has to reach
    /// <see cref="PresenceFreshness.Unknown"/> on its own rather than waiting for a read
    /// to succeed and say so.
    /// </summary>
    public class PresenceFreshnessTrackerTests
    {
        private static readonly DateTime Start = new DateTime(2026, 9, 9, 9, 0, 0, DateTimeKind.Local);
        private static readonly TimeSpan UnknownAfter = TimeSpan.FromSeconds(120);

        private static PresenceFreshnessTracker Tracker() => new PresenceFreshnessTracker(UnknownAfter);

        [Fact]
        public void A_successful_read_is_fresh()
        {
            var tracker = Tracker();

            Assert.Equal(PresenceFreshness.Fresh, tracker.RecordSuccess(Start));
            Assert.Equal(Start, tracker.LastSuccessAt);
        }

        [Fact]
        public void One_failure_holds_the_last_status_rather_than_reacting_to_it()
        {
            var tracker = Tracker();
            tracker.RecordSuccess(Start);

            Assert.Equal(PresenceFreshness.Holding, tracker.RecordFailure(Start.AddSeconds(5)));
        }

        [Fact]
        public void Failures_within_the_window_keep_holding()
        {
            var tracker = Tracker();
            tracker.RecordSuccess(Start);

            for (int second = 5; second < 120; second += 5)
            {
                Assert.Equal(PresenceFreshness.Holding, tracker.RecordFailure(Start.AddSeconds(second)));
            }
        }

        [Fact]
        public void The_window_is_inclusive_so_the_configured_threshold_itself_is_unknown()
        {
            var tracker = Tracker();
            tracker.RecordSuccess(Start);

            Assert.Equal(PresenceFreshness.Holding, tracker.RecordFailure(Start.Add(UnknownAfter).AddSeconds(-1)));
            Assert.Equal(PresenceFreshness.Unknown, tracker.RecordFailure(Start.Add(UnknownAfter)));
        }

        [Fact]
        public void Failing_past_the_window_becomes_unknown_and_stays_unknown()
        {
            var tracker = Tracker();
            tracker.RecordSuccess(Start);

            Assert.Equal(PresenceFreshness.Unknown, tracker.RecordFailure(Start.AddSeconds(121)));
            Assert.Equal(PresenceFreshness.Unknown, tracker.RecordFailure(Start.AddSeconds(600)));
        }

        [Fact]
        public void A_read_that_succeeds_again_clears_unknown()
        {
            var tracker = Tracker();
            tracker.RecordSuccess(Start);
            tracker.RecordFailure(Start.AddSeconds(300));

            Assert.Equal(PresenceFreshness.Fresh, tracker.RecordSuccess(Start.AddSeconds(305)));
            Assert.Equal(Start.AddSeconds(305), tracker.LastSuccessAt);
            Assert.Equal(TimeSpan.Zero, tracker.UnconfirmedFor(Start.AddSeconds(305)));
        }

        [Fact]
        public void The_window_restarts_from_the_most_recent_success()
        {
            var tracker = Tracker();
            tracker.RecordSuccess(Start);
            tracker.RecordFailure(Start.AddSeconds(110));
            tracker.RecordSuccess(Start.AddSeconds(115));

            // 170 seconds after the first success, but only 55 after the second.
            Assert.Equal(PresenceFreshness.Holding, tracker.RecordFailure(Start.AddSeconds(170)));
        }

        [Fact]
        public void A_tracker_that_has_never_succeeded_starts_unknown()
        {
            Assert.Equal(PresenceFreshness.Unknown, Tracker().Current);
        }

        [Fact]
        public void Failing_from_the_first_attempt_reaches_unknown_on_the_same_schedule()
        {
            // A process that starts unable to reach Graph must still say so, otherwise it
            // leaves whatever colour the light was showing when it started.
            var tracker = Tracker();

            Assert.Equal(PresenceFreshness.Holding, tracker.RecordFailure(Start));
            Assert.Equal(PresenceFreshness.Holding, tracker.RecordFailure(Start.AddSeconds(119)));
            Assert.Equal(PresenceFreshness.Unknown, tracker.RecordFailure(Start.AddSeconds(120)));
            Assert.Null(tracker.LastSuccessAt);
        }

        [Fact]
        public void A_zero_window_makes_a_single_failure_unknown()
        {
            var tracker = new PresenceFreshnessTracker(TimeSpan.Zero);
            tracker.RecordSuccess(Start);

            Assert.Equal(PresenceFreshness.Unknown, tracker.RecordFailure(Start));
        }

        [Fact]
        public void A_negative_window_makes_a_single_failure_unknown()
        {
            var tracker = new PresenceFreshnessTracker(TimeSpan.FromSeconds(-30));
            tracker.RecordSuccess(Start);

            Assert.Equal(PresenceFreshness.Unknown, tracker.RecordFailure(Start));
        }

        [Fact]
        public void Changing_the_window_applies_to_the_next_read_without_a_restart()
        {
            var tracker = Tracker();
            tracker.RecordSuccess(Start);
            Assert.Equal(PresenceFreshness.Holding, tracker.RecordFailure(Start.AddSeconds(60)));

            tracker.UnknownAfter = TimeSpan.FromSeconds(30);

            Assert.Equal(PresenceFreshness.Unknown, tracker.RecordFailure(Start.AddSeconds(61)));
        }

        [Fact]
        public void A_new_tracker_has_not_attempted_anything()
        {
            // Current starts at Unknown, so this is the only way to tell a new tracker
            // from one that has given up on a real outage. Without it, the first
            // successful read of a healthy start is reported as a recovery.
            Assert.False(Tracker().HasAttempted);
        }

        [Fact]
        public void A_success_counts_as_an_attempt()
        {
            var tracker = Tracker();
            tracker.RecordSuccess(Start);

            Assert.True(tracker.HasAttempted);
        }

        [Fact]
        public void A_failure_counts_as_an_attempt_even_though_nothing_has_succeeded()
        {
            var tracker = Tracker();
            tracker.RecordFailure(Start);

            Assert.True(tracker.HasAttempted);
            Assert.Null(tracker.LastSuccessAt);
        }

        [Fact]
        public void Unconfirmed_time_is_measured_from_the_last_success()
        {
            var tracker = Tracker();
            tracker.RecordSuccess(Start);

            Assert.Equal(TimeSpan.FromSeconds(90), tracker.UnconfirmedFor(Start.AddSeconds(90)));
        }

        [Fact]
        public void Unconfirmed_time_is_zero_before_anything_has_been_attempted()
        {
            Assert.Equal(TimeSpan.Zero, Tracker().UnconfirmedFor(Start));
        }
    }
}
