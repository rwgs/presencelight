using System;

using Newtonsoft.Json;

namespace PresenceLight.Core
{
    /// <summary>
    /// Represents the settings for controlling the lights.
    /// </summary>
    public class LightSettings
    {
        /// <summary>
        /// Gets or sets the status to display after a certain number of hours have passed.
        /// </summary>
        public string HoursPassedStatus { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to synchronize the lights.
        /// </summary>
        public bool SyncLights { get; set; }

        /// <summary>
        /// Gets or sets the working days.
        /// </summary>
        public string WorkingDays { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use working hours.
        /// </summary>
        public bool UseWorkingHours { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use AM/PM format for working hours.
        /// </summary>
        public bool UseAmPm { get; set; }

        /// <summary>
        /// Gets or sets the start time of working hours.
        /// </summary>
        public string WorkingHoursStartTime { get; set; }

        /// <summary>
        /// Gets or sets the start time of working hours as a <see cref="DateTime"/> object.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        [JsonProperty(Required = Required.Default)]
        public DateTime? WorkingHoursStartTimeAsDate { get; set; }

        /// <summary>
        /// Gets or sets the end time of working hours as a <see cref="DateTime"/> object.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        [JsonProperty(Required = Required.Default)]
        public DateTime? WorkingHoursEndTimeAsDate { get; set; }

        /// <summary>
        /// Gets or sets the end time of working hours.
        /// </summary>
        public string WorkingHoursEndTime { get; set; }

        /// <summary>
        /// Gets or sets the polling interval in seconds.
        /// </summary>
        public double PollingInterval { get; set; }

        /// <summary>
        /// Gets or sets how many seconds presence may go unconfirmed before the light
        /// shows the presence-unknown status instead of its last known colour.
        /// </summary>
        /// <remarks>
        /// A light left on its last colour cannot be told apart from a correct one, so
        /// a failed or stale read must eventually say so rather than keep showing that
        /// someone is available. Zero or less makes a single failed read enough.
        /// </remarks>
        public double PresenceUnknownAfterSeconds { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use the default brightness.
        /// </summary>
        public bool UseDefaultBrightness { get; set; }

        /// <summary>
        /// Gets or sets the default brightness level.
        /// </summary>
        public int DefaultBrightness { get; set; }

        /// <summary>
        /// Gets or sets the custom API settings.
        /// </summary>
        public CustomApi CustomApi { get; set; }

        /// <summary>
        /// Gets or sets the local serial host settings.
        /// </summary>
        public LocalSerialHost LocalSerialHost { get; set; }

        /// <summary>
        /// Gets or sets the LIFX settings.
        /// </summary>
        public LIFX LIFX { get; set; }

        /// <summary>
        /// Gets or sets the Hue settings.
        /// </summary>
        public Hue Hue { get; set; }

        /// <summary>
        /// Gets or sets the Yeelight settings.
        /// </summary>
        public Yeelight Yeelight { get; set; }

        /// <summary>
        /// Gets or sets the Wiz settings.
        /// </summary>
        public Wiz Wiz { get; set; }
    }
}
