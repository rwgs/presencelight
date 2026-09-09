using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace PresenceLight.Core
{
    public interface ILocalSerialHostService
    {
        Task<string> SetColor(string availability, string? activity, CancellationToken cancellationToken = default);
        Task<IEnumerable<string>> GetPorts();
        void Initialize(AppState _appState);
    }


    public class LocalSerialHostService : ILocalSerialHostService
    {
        private MediatR.IMediator _mediator;
        private string _currentAvailability = string.Empty;
        private string _currentActivity = string.Empty;
        private SerialPort _port = null;
        private string _lineEnding = "";

        private readonly ILogger<LocalSerialHostService> _logger;
        private AppState _appState;
        private readonly object serialWriteLock = new object();

        public LocalSerialHostService(AppState appState, ILogger<LocalSerialHostService> logger, MediatR.IMediator mediator)
        {
            _logger = logger;
            _appState = appState;
            _mediator = mediator;

            switch (appState.Config.LightSettings.LocalSerialHost.LocalSerialHostMainSetup.LineEnding)
            {
                case "CR" :
                    _lineEnding = "\r";
                    break;
                case "LF" :
                    _lineEnding = "\n";
                    break;
                case "CRLF" :
                    _lineEnding = "\r\n";
                    break;
                default :
                    _logger.LogDebug("Line endings not set or empty string");
                    _lineEnding = "";
                    break;
            }

            if (!string.IsNullOrEmpty(appState.Config.LightSettings.LocalSerialHost.LocalSerialHostMainSetup.Port))
            {
                SetupSerialPort(appState.Config.LightSettings.LocalSerialHost.LocalSerialHostMainSetup.Port);
            }
        }

        ~LocalSerialHostService()
        {
            if (_port != null && _port.IsOpen)
            {
                _port.Close();
            }
        }

        public void Initialize(AppState appState)
        {
            _appState = appState;

            if (_port != null && _port.IsOpen)
            {
                _port.Close();
                _port  = null;
            }

            switch (appState.Config.LightSettings.LocalSerialHost.LocalSerialHostMainSetup.LineEnding)
            {
                case "CR" :
                    _lineEnding = "\r";
                    break;
                case "LF" :
                    _lineEnding = "\n";
                    break;
                case "CRLF" :
                    _lineEnding = "\r\n";
                    break;
                default :
                    _logger.LogDebug("Line endings not set or empty string");
                    _lineEnding = "";
                    break;
            }

            if (!string.IsNullOrEmpty(appState.Config.LightSettings.LocalSerialHost.LocalSerialHostMainSetup.Port))
            {
                SetupSerialPort(appState.Config.LightSettings.LocalSerialHost.LocalSerialHostMainSetup.Port);
            }
        }

        public async Task<string> SetColor(string availability, string? activity, CancellationToken cancellationToken = default)
        {
            string result = await SetAvailability(availability, cancellationToken);
            result += await SetActivity(activity, cancellationToken);
            return result;
        }

        private async Task<(bool Written, string Result)> CallLocalSerialHostForActivityChanged(object sender, string newActivity, CancellationToken cancellationToken)
        {
            string message = string.Empty;
            string result = string.Empty;

            switch (newActivity)
            {
                case "Available":
                    message = _appState.Config.LightSettings.LocalSerialHost.LocalSerialHostActivityAvailable;
                    break;
                case "Presenting":
                    message = _appState.Config.LightSettings.LocalSerialHost.LocalSerialHostActivityPresenting;
                    break;
                case "InACall":
                    message = _appState.Config.LightSettings.LocalSerialHost.LocalSerialHostActivityInACall;
                    break;
                case "InAConferenceCall":
                    message = _appState.Config.LightSettings.LocalSerialHost.LocalSerialHostActivityInAConferenceCall;
                    break;
                case "InAMeeting":
                    message = _appState.Config.LightSettings.LocalSerialHost.LocalSerialHostActivityInAMeeting;
                    break;
                case "Busy":
                    message = _appState.Config.LightSettings.LocalSerialHost.LocalSerialHostActivityBusy;
                    break;
                case "Away":
                    message = _appState.Config.LightSettings.LocalSerialHost.LocalSerialHostActivityAway;
                    break;
                case "BeRightBack":
                    message = _appState.Config.LightSettings.LocalSerialHost.LocalSerialHostActivityBeRightBack;
                    break;
                case "DoNotDisturb":
                    message = _appState.Config.LightSettings.LocalSerialHost.LocalSerialHostActivityDoNotDisturb;
                    break;
                case "Idle":
                    message = _appState.Config.LightSettings.LocalSerialHost.LocalSerialHostActivityIdle;
                    break;
                case "Offline":
                    message = _appState.Config.LightSettings.LocalSerialHost.LocalSerialHostActivityOffline;
                    break;
                case "Off":
                    message = _appState.Config.LightSettings.LocalSerialHost.LocalSerialHostActivityOff;
                    break;
                default:
                    break;
            }

            return await PerformSerialMessage(message, result, cancellationToken);
        }

        private async Task<(bool Written, string Result)> CallLocalSerialHostForAvailabilityChanged(object sender, string newAvailability, CancellationToken cancellationToken)
        {
            string message = string.Empty;
            string result = string.Empty;

            switch (newAvailability)
            {
                case "Available":
                    message = _appState.Config.LightSettings.LocalSerialHost.LocalSerialHostAvailable;
                    break;
                case "Busy":
                    message = _appState.Config.LightSettings.LocalSerialHost.LocalSerialHostBusy;
                    break;
                case "BeRightBack":
                    message = _appState.Config.LightSettings.LocalSerialHost.LocalSerialHostBeRightBack;
                    break;
                case "Away":
                    message = _appState.Config.LightSettings.LocalSerialHost.LocalSerialHostAway;
                    break;
                case "DoNotDisturb":
                    message = _appState.Config.LightSettings.LocalSerialHost.LocalSerialHostDoNotDisturb;
                    break;
                case "AvailableIdle":
                    message = _appState.Config.LightSettings.LocalSerialHost.LocalSerialHostAvailableIdle;
                    break;
                case "Offline":
                    message = _appState.Config.LightSettings.LocalSerialHost.LocalSerialHostOffline;
                    break;
                case "Off":
                    message = _appState.Config.LightSettings.LocalSerialHost.LocalSerialHostOff;
                    break;
                default:
                    break;
            }

            return await PerformSerialMessage(message, result, cancellationToken);
        }

        private async Task<string> SetAvailability(string availability, CancellationToken cancellationToken)
        {
            string result = string.Empty;
            if (availability != _currentAvailability)
            {
                bool written;
                (written, result) = await CallLocalSerialHostForAvailabilityChanged(this, availability, cancellationToken);
                if (!cancellationToken.IsCancellationRequested && written)
                {
                    _currentAvailability = availability;
                }
                else
                {
                    // Operation was cancelled, or the write did not reach the port.
                    // Leaving _currentAvailability alone is what allows the next poll
                    // to try again, and reopen the port on the way.
                }

            }
            else
            {
                // availability did not change: don't spam call the api
            }
            return result;
        }

        private async Task<string> SetActivity(string activity, CancellationToken cancellationToken)
        {
            string result = string.Empty;
            if (activity != _currentActivity)
            {
                bool written;
                (written, result) = await CallLocalSerialHostForActivityChanged(this, activity, cancellationToken);
                if (!cancellationToken.IsCancellationRequested && written)
                {
                    _currentActivity = activity;
                }
                else
                {
                    // Operation was cancelled, or the write did not reach the port.
                    // Leaving _currentActivity alone is what allows the next poll to
                    // try again, and reopen the port on the way.
                }
            }
            else
            {
                // activity did not change: don't spam call the api
            }
            return result;
        }

        static Stack<string> _lastLineEndingCalled = new Stack<string>(1);
        private async Task<(bool Written, string Result)> PerformSerialMessage(string serialMessage, string result, CancellationToken cancellationToken)
        {
            if (_lastLineEndingCalled.Contains($"{serialMessage}"))
            {
                _logger.LogDebug("No Change to State... NOT calling Api");
                return (true, "Skipped");
            }

            using (Serilog.Context.LogContext.PushProperty("message", serialMessage))
            {
                if (string.IsNullOrEmpty(serialMessage))
                {
                    // Nothing is configured for this status, so there is nothing to retry.
                    return (true, result);
                }

                try
                {
                    if (_port == null || !_port.IsOpen)
                    {
                        _logger.LogWarning("Serial Port not setup in PerformSerialMessage. Attempting to initialize");
                        SetupSerialPort(_appState.Config.LightSettings.LocalSerialHost.LocalSerialHostMainSetup.Port);
                    }

                    Task<(bool Written, string Result)> writeTask = Task.Run(() =>
                    {
                        string writeResult = "";
                        bool written = false;
                        try
                        {
                            lock (serialWriteLock)
                            {
                                _port.Write(serialMessage + _lineEnding);

                                // The message has reached the port. A device that never
                                // replies is not a failed write, so a read timeout must
                                // not discard that.
                                written = true;

                                try
                                {
                                    writeResult = _port.ReadLine();
                                }
                                catch (TimeoutException)
                                {
                                    _logger.LogDebug("Serial device did not reply within the read timeout");
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, "Error Performing Serial Write");
                            writeResult = $"Error: {e.Message}";
                        }

                        return (written, writeResult.Trim());
                    });

                    string message = $"Sending {serialMessage} to {_port.PortName}";
                    bool written;
                    (written, result) = await Task.WhenAny(writeTask).Result;

                    _logger.LogInformation(message);

                    // Only remember the message once it has actually been written.
                    // Recording a failed write is what stopped the state from being sent
                    // again after the port came back.
                    if (written)
                    {
                        _lastLineEndingCalled.TryPop(out string res);
                        _lastLineEndingCalled.Push($"{serialMessage}");
                    }

                    using (Serilog.Context.LogContext.PushProperty("result", result))
                        _logger.LogDebug(message + " Results");

                    return (written, result);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error Performing Web Request");
                    return (false, $"Error: {e.Message}");
                }
            }
        }

        public async Task<IEnumerable<string>> GetPorts()
        {
            try 
            {
                IEnumerable<string> ports = await Task.Run(() => SerialPort.GetPortNames());
                return ports;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to retrieve serial ports");
                throw;
            }
        }

        private void SetupSerialPort(string serialPort)
        {
        if (!string.IsNullOrEmpty(serialPort) && serialPort != "null")
            {                
                _port = new SerialPort();
                _port.PortName = serialPort;
                _port.ReadTimeout = 500;
                _port.WriteTimeout = 500;
                int baudRateInternal = 0;
                if (int.TryParse(_appState.Config.LightSettings.LocalSerialHost.LocalSerialHostMainSetup.BaudRate, out baudRateInternal))
                {
                    _port.BaudRate = baudRateInternal;
                }
                else
                {
                    _port.BaudRate = 9600;
                }

                if (!_port.IsOpen)
                {
                    _port.Open();
                }
            }
        }
    }
}
