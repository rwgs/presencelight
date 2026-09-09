
using System;
using System.Reflection;

namespace PresenceLight
{
    public static class PresenceConstants
    {
        public const string Inactive = "Not Logged In";
    }

    public class PresenceColors
    {
        public const string Available = "#009933";
        public const string AvailableIdle = "#FFFF00";
        public const string Busy = "#FF3300";
        public const string BusyIdle = "#FFFF00";
        public const string BeRightBack = "#FFFF00";
        public const string Away = "#FFFF00";
        public const string DoNotDisturb = "#B03CDE";
        public const string OutOfOffice = "#800080";
        public const string Offline = "#FFFFFF";
        public const string Inactive = "#FFFFFF";

        public static string GetColor(string status)
        {
            var pc = new PresenceColors();
            Type type =pc.GetType();
            PropertyInfo[] props = type.GetProperties();

            foreach (var prop in props)
            {
                if (prop.Name == status)
                {
                    return prop.GetValue(pc).ToString();
                }
            }
            return PresenceColors.Inactive;
        }
    }

    public static class IconConstants
    {
        private static string Base = "pack://application:,,,/PresenceLight;component/icons/";

        // Not every status has an icon, and the transparent set is missing two that the
        // standard set has, so a status without one has to fall back. Asking for a
        // missing pack resource throws, and that exception comes out of the tray update
        // rather than anywhere it can be handled usefully.
        private static readonly string[] StandardIcons =
        {
            "Available", "AvailableIdle", "Away", "BeRightBack", "Busy", "BusyIdle",
            "DoNotDisturb", "Inactive", "Offline", "OutOfOffice"
        };

        private static readonly string[] TransparentIcons =
        {
            "Available", "AvailableIdle", "Away", "BeRightBack", "Busy", "BusyIdle",
            "DoNotDisturb", "OutOfOffice"
        };

        public static string GetIcon(string iconType, string status)
        {
            if (string.IsNullOrEmpty(status))
            {
                status = "Inactive";
            }

            if (iconType == "Transparent" && Array.Exists(TransparentIcons, i => i == status))
            {
                return $"{Base}t_{status}.ico";
            }

            // Falls back to the standard set rather than to nothing, so a transparent
            // icon that was never drawn still shows the right status.
            if (Array.Exists(StandardIcons, i => i == status))
            {
                return $"{Base}{status}.ico";
            }

            return $"{Base}Inactive.ico";
        }
    }
}
