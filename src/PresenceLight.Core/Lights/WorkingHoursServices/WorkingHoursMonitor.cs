using System;

namespace PresenceLight.Core.WorkingHoursServices
{
    /// <summary>
    /// What the working hours schedule is doing to the light.
    /// </summary>
    public enum WorkingHoursState
    {
        /// <summary>The schedule is switched off, so presence is followed at any time.</summary>
        NotInUse,

        /// <summary>Inside the configured hours, so presence is followed.</summary>
        Following,

        /// <summary>Outside the configured hours, so presence is not followed.</summary>
        Suppressed
    }

    /// <summary>
    /// What the polling loop should do to the light on this pass.
    /// </summary>
    public enum WorkingHoursAction
    {
        /// <summary>Leave the light where the end of the working day put it.</summary>
        None,

        /// <summary>Set the light from presence.</summary>
        FollowPresence,

        /// <summary>Apply the configured end of working day action.</summary>
        EndOfDay
    }

    /// <summary>
    /// What the working hours schedule decided on one pass of the polling loop.
    /// </summary>
    public readonly struct WorkingHoursDecision
    {
        internal WorkingHoursDecision(WorkingHoursState state, WorkingHoursAction action, bool stateChanged)
        {
            State = state;
            Action = action;
            StateChanged = stateChanged;
        }

        /// <summary>What the schedule is doing to the light.</summary>
        public WorkingHoursState State { get; }

        /// <summary>What the loop should do about it on this pass.</summary>
        public WorkingHoursAction Action { get; }

        /// <summary>
        /// Whether <see cref="State"/> differs from the previous pass, counting the first
        /// pass of a session as a change. Reporting on this rather than on every pass puts
        /// one line in the log per change instead of one per poll.
        /// </summary>
        public bool StateChanged { get; }
    }

    /// <summary>
    /// Decides what the working hours schedule should do to the light, from whether the
    /// schedule is switched on and whether the moment falls inside it.
    /// </summary>
    /// <remarks>
    /// The end of working day action has to be applied on the way out of working hours
    /// rather than on every pass, so the decision has to remember what the previous pass
    /// saw. Keeping that memory as loose booleans in the polling loop, consulted only
    /// once the schedule was known to be switched on, is what made switching the schedule
    /// off and on again leave the light wherever presence had last put it: the loop still
    /// held the reading it took before the schedule was switched off, so switching it back
    /// on outside working hours found nothing to transition from, never applied the end of
    /// day action, and never reported that presence had stopped being followed. Whether
    /// the schedule is in use is therefore one of the states being tracked rather than a
    /// test applied before consulting the state.
    /// </remarks>
    public class WorkingHoursMonitor
    {
        private WorkingHoursState? _state;
        private bool _endOfDayApplied;

        /// <param name="useWorkingHours">Whether the schedule is switched on.</param>
        /// <param name="isInWorkingHours">Whether now falls inside the configured hours.</param>
        public WorkingHoursDecision Next(bool useWorkingHours, bool isInWorkingHours)
        {
            WorkingHoursState state = !useWorkingHours
                ? WorkingHoursState.NotInUse
                : isInWorkingHours
                    ? WorkingHoursState.Following
                    : WorkingHoursState.Suppressed;

            bool changed = _state != state;
            _state = state;

            if (state != WorkingHoursState.Suppressed)
            {
                // Leaving the suppressed state, whether because the hours started or
                // because the schedule was switched off, arms the end of day action for
                // the next time it is entered.
                _endOfDayApplied = false;
                return new WorkingHoursDecision(state, WorkingHoursAction.FollowPresence, changed);
            }

            if (_endOfDayApplied)
            {
                return new WorkingHoursDecision(state, WorkingHoursAction.None, changed);
            }

            _endOfDayApplied = true;
            return new WorkingHoursDecision(state, WorkingHoursAction.EndOfDay, changed);
        }
    }
}
