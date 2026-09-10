using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using HueApi;
using HueApi.BridgeLocator;
using HueApi.ColorConverters.Original.Extensions;
using HueApi.Models;
using HueApi.Models.Exceptions;
using HueApi.Models.Requests;

using Microsoft.Extensions.Logging;


namespace PresenceLight.Core
{
    public interface IHueService
    {
        Task SetColor(string availability, string activity, string lightId);
        Task<string> RegisterBridge();
        Task<IEnumerable<Light>> GetLights();

        Task<IEnumerable<GroupedLight>> GetGroups();
        Task<string> FindBridge();
        void Initialize(AppState appState);
    }
    public class HueService : IHueService
    {
        private AppState _appState;
        private LocalHueApi _client;
        private readonly ILogger<HueService> _logger;

        /// <summary>
        /// The selection last reported as missing from the bridge, so that a light which
        /// stays missing is reported once rather than on every write.
        /// </summary>
        private string _missingLightId;

        public HueService(AppState appState, ILogger<HueService> logger)
        {
            _logger = logger;
            _appState = appState;
        }

        public void Initialize(AppState appState)
        {
            _appState = appState;
        }

        public async Task SetColor(string availability, string activity, string lightId)
        {
            if (_appState.HueLights == null || _appState.HueLights.Count() == 0)
            {
                if (lightId.Contains("group_id:"))
                {
                    _appState.SetHueLights(await GetGroups());
                }
                else
                {
                    _appState.SetHueLights(await GetLights());
                }
            }

            if (string.IsNullOrEmpty(lightId))
            {
                _logger.LogInformation("Selected Hue Light Not Specified");
                return;
            }

            try
            {
                _client = new LocalHueApi(_appState.Config.LightSettings.Hue.HueIpAddress, _appState.Config.LightSettings.Hue.HueApiKey);

                var o = Handle(_appState.Config.LightSettings.Hue.UseActivityStatus ? activity : availability, lightId);

                if (o.turnOff)
                {
                    await TurnOff(lightId);
                    return;
                }

                var color = o.color.Replace("#", "");
                var command = o.command;
                switch (color.Length)
                {
                    case var length when color.Length == 6:
                        // Do Nothing
                        break;
                    case var length when color.Length > 6:
                        // Get last 6 characters
                        color = color.Substring(0, 6);
                        break;
                    default:
                        throw new ArgumentException("Supplied Color had an issue");
                }

                var rgbColor = new HueApi.ColorConverters.RGBColor(color);
                // Set the color using extension method
                command.SetColor(rgbColor);



                if (availability == "Off")
                {
                    await TurnOff(lightId);
                    return;
                }

                if (_appState.Config.LightSettings.UseDefaultBrightness)
                {
                    if (_appState.Config.LightSettings.DefaultBrightness == 0)
                    {
                        command.TurnOff();
                    }
                    else
                    {
                        command.TurnOn();
                        command.Dimming = new Dimming { Brightness = Convert.ToDouble(_appState.Config.LightSettings.DefaultBrightness) };
                        command.Dynamics = new Dynamics { Duration = 0 };
                    }
                }
                else
                {
                    if (_appState.Config.LightSettings.Hue.Brightness == 0)
                    {
                        command.TurnOff();
                    }
                    else
                    {
                        command.TurnOn();
                        command.Dimming = new Dimming { Brightness = Convert.ToDouble(_appState.Config.LightSettings.Hue.Brightness) };
                        command.Dynamics = new Dynamics { Duration = 0 };
                    }
                }

                Guid? bridgeId = await ResolveBridgeId(lightId);
                if (bridgeId is null)
                {
                    return;
                }

                if (IsGroup(lightId))
                {
                    var groupCommand = new UpdateGroupedLight();
                    groupCommand.Color = command.Color;
                    groupCommand.On = command.On;
                    groupCommand.Dimming = command.Dimming;
                    groupCommand.Dynamics = command.Dynamics;
                    await _client.GroupedLight.UpdateAsync(bridgeId.Value, groupCommand);
                }
                else
                {
                    await _client.Light.UpdateAsync(bridgeId.Value, command);
                }

                _logger.LogInformation($"Setting Hue Light {lightId} to {color}");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error Occurred Setting Color");
                throw;
            }
        }

        // The bridge only accepts a registration while its link button is pressed, so a
        // single attempt fails whenever it races the press. Keep asking for the length of
        // the bridge's link window instead, which lets the button be pressed before or
        // after the request starts.
        private static readonly TimeSpan LinkButtonWindow = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan LinkButtonRetryInterval = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Turns the selected light or group off.
        /// </summary>
        private async Task TurnOff(string lightId)
        {
            Guid? bridgeId = await ResolveBridgeId(lightId);
            if (bridgeId is null)
            {
                return;
            }

            if (IsGroup(lightId))
            {
                var groupCommand = new UpdateGroupedLight();
                groupCommand.TurnOff();
                await _client.GroupedLight.UpdateAsync(bridgeId.Value, groupCommand);
            }
            else
            {
                var command = new UpdateLight();
                command.TurnOff();
                await _client.Light.UpdateAsync(bridgeId.Value, command);
            }

            _logger.LogInformation($"Turning Hue Light {lightId} Off");
        }

        /// <summary>
        /// The bridge's own identifier for the selected light or group, or null if the
        /// bridge does not have it.
        /// </summary>
        /// <remarks>
        /// A selection is a v1 identifier such as <c>id:/lights/4</c>, and the v2 API is
        /// addressed by a Guid, so it has to be looked up in the list the bridge
        /// returned. Turning a light off parsed the selection as a Guid instead, which
        /// cannot succeed: exiting the application, reaching the end of the working day
        /// with the after-hours action set to Off, and configuring a status as disabled
        /// all threw a format error and left the light on its last colour. Observed on
        /// the installed build with a selection of <c>id:/lights/4</c>.
        ///
        /// The cached list is refreshed on a miss, because it is only fetched when empty
        /// and can hold the wrong kind: the settings page fills it with groups when
        /// grouped control is chosen and with individual lights otherwise, so a selection
        /// made in one mode was looked up in the other mode's list. That threw an invalid
        /// cast out of every write for the life of the process, with nothing to refetch
        /// it, which is the light-side half of recovery that upstream issue 973 describes
        /// for serial devices.
        /// </remarks>
        private async Task<Guid?> ResolveBridgeId(string lightId)
        {
            Guid? resolved = MatchBridgeId(_appState.HueLights, lightId);

            if (resolved is null)
            {
                _appState.SetHueLights(IsGroup(lightId)
                    ? (IEnumerable<object>) await GetGroups()
                    : await GetLights());

                resolved = MatchBridgeId(_appState.HueLights, lightId);
            }

            if (resolved is null)
            {
                // Reported once per selection rather than on every write, because the
                // loop writes the colour every few seconds for as long as it is running.
                if (_missingLightId != lightId)
                {
                    _missingLightId = lightId;
                    _logger.LogWarning($"Selected Hue light {lightId} is not on the bridge; nothing was set");
                }
            }
            else
            {
                _missingLightId = null;
            }

            return resolved;
        }

        /// <summary>
        /// Finds the selection in a list the bridge returned, without asking it again.
        /// </summary>
        /// <param name="lights">The cached list, which may hold either kind or neither.</param>
        /// <param name="lightId">The configured selection, such as <c>id:/lights/4</c>.</param>
        private static Guid? MatchBridgeId(IEnumerable<object> lights, string lightId)
        {
            if (lights is null || string.IsNullOrEmpty(lightId))
            {
                return null;
            }

            // OfType rather than a cast, so a list holding the other kind misses and is
            // refreshed rather than throwing.
            return IsGroup(lightId)
                ? lights.OfType<GroupedLight>().FirstOrDefault(g => g.IdV1 == lightId.Replace("group_id:", ""))?.Id
                : lights.OfType<Light>().FirstOrDefault(l => l.IdV1 == lightId.Replace("id:", ""))?.Id;
        }

        /// <summary>
        /// Whether the selection names a group rather than a single light.
        /// </summary>
        private static bool IsGroup(string lightId) => lightId.Contains("group_id:");

        //Need to wire up a way to do this without user intervention
        public async Task<string> RegisterBridge()
        {
            if (!string.IsNullOrEmpty(_appState.Config.LightSettings.Hue.HueApiKey))
            {
                return _appState.Config.LightSettings.Hue.HueApiKey;
            }

            _logger.LogInformation("Registering with Hue Bridge - Please press the button on your bridge");

            DateTime giveUpAt = DateTime.UtcNow.Add(LinkButtonWindow);
            while (true)
            {
                try
                {
                    var result = await LocalHueApi.RegisterAsync(_appState.Config.LightSettings.Hue.HueIpAddress, "PresenceLight", Environment.MachineName, true);
                    if (!string.IsNullOrEmpty(result?.Username)) // RegisterAsync returns RegisterEntertainmentResult with Username property
                    {
                        return result.Username;
                    }
                }
                catch (LinkButtonNotPressedException)
                {
                    // Expected until the button is pressed; keep waiting for the link window.
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error Occurred Registering Bridge");
                    return String.Empty;
                }

                if (DateTime.UtcNow >= giveUpAt)
                {
                    _logger.LogError("Hue Bridge link button was not pressed within {Seconds} seconds", LinkButtonWindow.TotalSeconds);
                    return String.Empty;
                }

                await Task.Delay(LinkButtonRetryInterval);
            }
        }

        public async Task<string> FindBridge()
        {
            try
            {
                HttpBridgeLocator locator = new HttpBridgeLocator();
                var bridges = await locator.LocateBridgesAsync(TimeSpan.FromSeconds(5));
                if (bridges.Any())
                {
                    return bridges.FirstOrDefault().IpAddress;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error Occurred Finding Bridge");
                return String.Empty;
            }
            return String.Empty;
        }

        public async Task<IEnumerable<Light>> GetLights()
        {
            try
            {
                if (_client == null)
                {
                    _client = new LocalHueApi(_appState.Config.LightSettings.Hue.HueIpAddress, _appState.Config.LightSettings.Hue.HueApiKey);
                }
                var lightsResponse = await _client.Light.GetAllAsync();
                return lightsResponse.Data;
            }
            catch (Exception e)
            {
                _logger.LogError(e, message: "Error Occurred Getting Lights");
                throw;
            }
        }

        public async Task<IEnumerable<GroupedLight>> GetGroups()
        {
            try
            {
                if (_client == null)
                {
                    _client = new LocalHueApi(_appState.Config.LightSettings.Hue.HueIpAddress, _appState.Config.LightSettings.Hue.HueApiKey);
                }
                var groupsResponse = await _client.GroupedLight.GetAllAsync();
                return groupsResponse.Data;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error Occurred Getting Groups");
                throw;
            }
        }

        /// <summary>
        /// Works out what the configured status says the light should do.
        /// </summary>
        /// <returns>
        /// The colour to show, the command to send, and whether the status is configured
        /// as disabled, which means turn the light off and show no colour at all.
        /// </returns>
        private (string color, UpdateLight command, bool turnOff) Handle(string presence, string lightId)
        {
            var props = _appState.Config.LightSettings.Hue.Statuses.GetType().GetProperties().ToList();

            if (_appState.Config.LightSettings.Hue.UseActivityStatus)
            {
                props = props.Where(a => a.Name.ToLower().StartsWith("activity")).ToList();
            }
            else
            {
                props = props.Where(a => a.Name.ToLower().StartsWith("availability")).ToList();
            }

            string color = "";
            var command = new UpdateLight();

            if (presence.Contains('#'))
            {
                // provided presence is actually a custom color
                color = presence;
                command.TurnOn();
                return (color, command, false);
            }

            foreach (var prop in props)
            {
                if (presence == prop.Name.Replace("Status", "").Replace("Availability", "").Replace("Activity", ""))
                {
                    var value = (AvailabilityStatus)prop.GetValue(_appState.Config.LightSettings.Hue.Statuses);

                    if (!value.Disabled)
                    {
                        command.TurnOn();
                        color = value.Color;
                        return (color, command, false);
                    }
                    else
                    {
                        // Reported rather than written here, so that every write to the
                        // bridge goes through the one place that knows how to address it.
                        command.TurnOff();
                        return (color, command, true);
                    }
                }
            }
            return (color, command, false);
        }
    }
}
