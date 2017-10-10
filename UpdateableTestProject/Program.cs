using System;
using System.Linq;
using ClientDeploy;

namespace UpdateableTestProject
{
    class Program
    {
        private static Updater _updater;

        static void Main(string[] args)
        {
            Console.Out.WriteLine("Updateable Test Project starting: " + (String.Join(" ",args)));

            var addendum =
                System.IO.File.Exists("myversion.txt")
                    ? $", with config file of {System.IO.File.ReadAllText("myversion.txt")}"
                    : "";

            Console.Out.WriteLine($"This is Version 2{addendum}.");

            _updater = ClientDeploy.Updater.Create(info => Console.WriteLine($"WARNING: {info}!"));

            if (_updater.UpdatesAvailable())
            {
                UpdateAvailable();
            }
            else
            {
                Console.Out.WriteLine("No updates available right now... Checking again in 30 Seconds...");
            }

            _updater.SchedulePeriodicUpdateChecks(TimeSpan.FromSeconds(30), UpdateAvailable);

            Console.Out.WriteLine("Press enter to exit...");
            Console.ReadLine();
            _updater.Dispose();

        }

        private static void UpdateAvailable()
        {
            Console.Out.WriteLine($"We have been informed, there is an update available.");
            Console.Out.WriteLine($"Version: {_updater.AvailableVersion()}");
            Console.Out.WriteLine($"Infos: {_updater.AvailableVersionReleaseNotes()}");

            _updater.UpdateNow(info => Console.WriteLine($"... {info}"));
        }
    }
}
