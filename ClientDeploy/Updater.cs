using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Timers;

namespace ClientDeploy
{
    public class Updater
    {
        private readonly Action<string> _onWarning;
        private Timer _timer;
        private string _updaterExecutable;

        private string Semver(string v)
        {
            return String.Join(".", v.Split('.').Select(_ => _.PadLeft(10, '0')));
        }

        private const string CONFIGFILENAME = ".clientdeploy.config";

        string uuid = null;
        string version = null;
        string repo = null;
        string product = null;

        private bool ready = false;
        private string _updaterversion;

        public static Updater Create(Action<string> onWarning)
        {
            return new Updater(onWarning);
        }

        private Updater() { }

        private Updater(Action<string> onWarning)
        {
            _onWarning = onWarning;
            Connect();
        }

        private void Connect()
        {
            try
            {
                var location = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                var configfile = System.IO.Path.Combine(location, CONFIGFILENAME);
                if (!System.IO.File.Exists(configfile))
                {
                    _onWarning(
                        $"Configuration file for ClientDeploy Installation not found at '{configfile}'. This software will not receive automated updates!");
                    return;
                }

                var config =
                    System.IO.File.ReadAllLines(configfile)
                        .Select(_ => _.Trim())
                        .Where(_ => !_.StartsWith("#"))
                        .Select(_ => _.Split(new[] { '|' }, 2)).ToDictionary(kv => kv[0], kv => kv[1]);

                if (!config.ContainsKey("uuid"))
                {
                    _onWarning(
                        $"Configuration file for ClientDeploy Installation has no uuid. This software will not receive automated updates!");
                    return;
                }
                if (!config.ContainsKey("repo"))
                {
                    _onWarning(
                        $"Configuration file for ClientDeploy Installation has no repo. This software will not receive automated updates!");
                    return;
                }
                if (!config.ContainsKey("product"))
                {
                    _onWarning(
                        $"Configuration file for ClientDeploy Installation has no product information. This software will not receive automated updates!");
                    return;
                }
                if (!config.ContainsKey("version"))
                {
                    _onWarning(
                        $"Configuration file for ClientDeploy Installation has no version information. This software will not receive automated updates!");
                    return;
                }

                uuid = config["uuid"];
                repo = config["repo"];
                product = config["product"];
                version = config["version"];

                var updaterpath = System.IO.Path.Combine(location, ".clientdeploy", "updater");
                _updaterversion = System.IO.Directory.GetDirectories(updaterpath)
                                      .Select(_ => _.Split('\\').Last())
                                      .OrderByDescending(Semver).FirstOrDefault() ?? "";

                _updaterExecutable = System.IO.Path.Combine(location, ".clientdeploy", "updater", _updaterversion, "ClientDeployUpdateProcess.exe");
                if (!System.IO.File.Exists(_updaterExecutable))
                {
                    _onWarning(
                        $"Executable for ClientDeploy Update System not found at '{_updaterExecutable}'. This software will not receive automated updates!");
                    return;
                }

                ready = true;
                if (UpdaterUpdatesAvailable()) updateTheUpdater();
            }
            catch (Exception ex)
            {
                _onWarning($"Error while accessing update system. This software will not receive automated updates! [{ex.Message}//{ex.GetType().Name}]");
                _onWarning(ex.ToString());
            }
        }

        private void updateTheUpdater()
        {
            ready = false;

            var args = $"--read version --repository {repo} --product ClientDeployUpdateProcess";

            var psi = new ProcessStartInfo(_updaterExecutable, args)
            {
                ErrorDialog = false,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            var process = Process.Start(psi);
            if (process == null)
            {
                return;
            }

            var newversion = process.StandardOutput.ReadToEnd().Trim();

            var uargs = $"--install .clientdeploy\\updater\\{newversion} --repository {repo} --product ClientDeployUpdateProcess";
            var upsi = new ProcessStartInfo(_updaterExecutable, uargs);
            var uprocess = Process.Start(upsi);
            if (uprocess == null) return;

            uprocess.WaitForExit(45000);


            var location = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            Console.Out.WriteLine(location);
            Console.Out.WriteLine(newversion);
            var updaterExecutable = location + "\\.clientdeploy\\updater\\" + newversion + "\\ClientDeployUpdateProcess.exe";
            Console.Out.WriteLine(updaterExecutable);
            if (System.IO.File.Exists(updaterExecutable))
            {
                var oldversion = _updaterversion;
                _updaterversion = newversion;
                _updaterExecutable = updaterExecutable;
                System.IO.Directory.Delete(location + "\\.clientdeploy\\updater\\" + oldversion, true);
            }

            ready = true;
        }

        public void SchedulePeriodicUpdateChecks(TimeSpan periodicCheckForUpdates, Action onUpdatesAvailable)
        {
            if (!ready)
            {
                _onWarning("ClientDeploy Update System not functioning. This software will not receive updates!");
                return;
            }

            _timer = new System.Timers.Timer();
            _timer.Elapsed += ((s, a) =>
            {
                try
                {
                    if (!ready) Connect();
                    if (!ready) return;
                    if (UpdaterUpdatesAvailable()) updateTheUpdater();
                    if (UpdatesAvailable()) onUpdatesAvailable();
                }
                catch (Exception ex)
                {
                    _onWarning(
                        $"Error while accessing update system. This software will not receive automated updates! [{ex.Message}//{ex.GetType().Name}]");
                    _onWarning(ex.ToString());
                    ready = false;
                }
            });
            _timer.AutoReset = true;
            _timer.Interval = periodicCheckForUpdates.TotalMilliseconds;
            _timer.Start();
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

        public bool UpdaterUpdatesAvailable()
        {
            if (!ready)
            {
                return false;
            }
            var args = $"--check {_updaterversion} --repository {repo} --product ClientDeployUpdateProcess";

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
            var entry = Assembly.GetEntryAssembly().Location;
            var cargs = String.Join(" ", System.Environment.GetCommandLineArgs());

            var args = $"--install . --repository {repo} --product {product} --kill {pid} --start \"{entry}\" --args \"{cargs}\"";

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
