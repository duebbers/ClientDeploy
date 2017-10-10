using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace ClientDeploy
{
    public class Updater
    {
        private readonly Action<string> _onWarning;
        private readonly Action<string> _onUpdateAvailable;
        private readonly TimeSpan? _periodicCheckForUpdates;
        private Timer _timer;
        private string _updaterExecutable;

        private const string CONFIGFILENAME = ".clientdeploy.config";

        string uuid = null;
        string version = null;
        string repo = null;
        string product = null;

        private bool ready = false;

        public static Updater Create(Action<string> onWarning)
        {
            return new Updater(onWarning);
        }

        private Updater() { }

        private Updater(Action<string> onWarning)
        {
            try
            {
                _onWarning = onWarning;

                var location = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                var configfile = System.IO.Path.Combine(location, CONFIGFILENAME);
                if (!System.IO.File.Exists(configfile))
                {
                    onWarning(
                        $"Configuration file for ClientDeploy Installation not found at '{configfile}'. This software will not receive automated updates!");
                    return;
                }

                var config =
                    System.IO.File.ReadAllLines(configfile)
                        .Select(_ => _.Trim())
                        .Where(_ => !_.StartsWith("#"))
                        .Select(_ => _.Split(new[] {'|'}, 2)).ToDictionary(kv => kv[0], kv => kv[1]);

                if (!config.ContainsKey("uuid"))
                {
                    onWarning(
                        $"Configuration file for ClientDeploy Installation has no uuid. This software will not receive automated updates!");
                    return;
                }
                if (!config.ContainsKey("repo"))
                {
                    onWarning(
                        $"Configuration file for ClientDeploy Installation has no uuid. This software will not receive automated updates!");
                    return;
                }
                if (!config.ContainsKey("product"))
                {
                    onWarning(
                        $"Configuration file for ClientDeploy Installation has no product information. This software will not receive automated updates!");
                    return;
                }
                if (!config.ContainsKey("version"))
                {
                    onWarning(
                        $"Configuration file for ClientDeploy Installation has no version information. This software will not receive automated updates!");
                    return;
                }

                uuid = config["uuid"];
                repo = config["repo"];
                product = config["product"];
                version = config["version"];

                // LOCATE UPDATER PROCESS
                // IF NOT FOUND, TRY TO DOWNLOAD

                var updater = System.IO.Path.Combine(location, ".clientdeploy", ".updater");
                if (!System.IO.File.Exists(updater))
                {
                    onWarning(
                        $"Configuration file for ClientDeploy Update System not found at '{updater}'. This software will not receive automated updates!");
                    return;
                }

                var u = System.IO.File.ReadAllText(updater);
                _updaterExecutable = System.IO.Path.Combine(location, ".clientdeploy", u);
                if (!System.IO.File.Exists(_updaterExecutable))
                {
                    onWarning(
                        $"Executable for ClientDeploy Update System not found at '{_updaterExecutable}'. This software will not receive automated updates!");
                    return;
                }

                ready = true;
            }
            catch (Exception ex)
            {
                onWarning($"Error while accessing update system. This software will not receive automated updates! [{ex.Message}//{ex.GetType().Name}]");
                onWarning(ex.ToString());
            }
        }

        public void SchedulePeriodicUpdateChecks(TimeSpan periodicCheckForUpdates, Action onUpdatesAvailable)
        {
            if (!ready)
            {
                _onWarning("ClientDeploy Update System not functioning. This software will not receive updates!");
                return;
            }

            _timer = new System.Threading.Timer(_ =>
            {
                try
                {
                    if (UpdatesAvailable()) onUpdatesAvailable();
                }
                catch (Exception ex)
                {
                    _onWarning($"Error while accessing update system. This software will not receive automated updates! [{ex.Message}//{ex.GetType().Name}]");
                    _onWarning(ex.ToString());
                    _timer.Dispose();
                    _timer = null;
                }
            }, null, periodicCheckForUpdates, periodicCheckForUpdates);
        }

        public bool UpdatesAvailable()
        {
            if (!ready)
            {
                _onWarning("ClientDeploy Update System not functioning. This software will not receive updates!");
                return false;
            }

            var args = $"--check {version} --repository {repo} --product {product}";

            var psi = new ProcessStartInfo(_updaterExecutable, args)
            {
                ErrorDialog = false,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            var process = Process.Start(psi);
            if (process == null)
            {
                _onWarning(
                    $"Unable to start ClientDeploy Update System at '{_updaterExecutable}'. This software will not receive automated updates!");
                return false;
            }

            var info = process.StandardOutput.ReadToEnd();
            if (info.StartsWith("#ERROR"))
            {
                if (info.Contains("TransientError"))
                {
                    _onWarning($"Unable to access Updates at the moment... ({info})");
                    return false;
                }
                else
                {
                    _onWarning($"Error in ClientDeploy Update System. This software will not receive automated updates! ({info})");
                    return false;
                }
            }

            if (info.StartsWith("#LATEST")) return false;

            if (info.StartsWith("#UPDATE")) return true;

            _onWarning($"Unexpected response from ClientDeploy Update System: {info}");
            return false;
        }

        public string AvailableVersion()
        {
            if (!ready)
            {
                return "ClientDeploy Update System not functioning. This software will not receive updates!";
            }

            var args = $"--read version --repository {repo} --product {product}";

            var psi = new ProcessStartInfo(_updaterExecutable, args)
            {
                ErrorDialog = false,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            var process = Process.Start(psi);
            if (process == null)
            {
                return "Update system unavailable at the moment...";
            }

            var info = process.StandardOutput.ReadToEnd();
            return info;
        }

        public string AvailableVersionReleaseNotes()
        {
            if (!ready)
            {
                return "ClientDeploy Update System not functioning. This software will not receive updates!";
            }

            var args = $"--read releasenotes --repository {repo} --product {product}";

            var psi = new ProcessStartInfo(_updaterExecutable, args)
            {
                ErrorDialog = false,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            var process = Process.Start(psi);
            if (process == null)
            {
                return "Update system unavailable at the moment...";
            }

            var info = process.StandardOutput.ReadToEnd();
            return info;
        }

        public void UpdateNow(Action<string> userInformation)
        {
            if (!ready)
            {
                return;
            }

            var pid = System.Diagnostics.Process.GetCurrentProcess().Id;

            var cmdline = System.Environment.CommandLine;

            var args = $"--install . --repository {repo} --product {product} --kill {pid} --start {cmdline}";

            var psi = new ProcessStartInfo(_updaterExecutable, args);

            userInformation("Beginning update...");

            var process = Process.Start(psi);
            if (process == null)
            {
                return;
            }

        }

        public void Dispose()
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}
