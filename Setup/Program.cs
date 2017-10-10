using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Setup
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Out.WriteLine("Setup from ClientDeploy");
                Console.Out.WriteLine("");
                Console.Out.WriteLine("setup <repo> <product> <target>");
                Console.Out.WriteLine("e.g. setup http://192.168.0.22:8081/repo SomeProduct .");
                return;
            }

            var repo = args[0];
            var product = args[1];
            var target = String.Join(" ", args.Skip(2));

            var cd = Path.Combine(target, ".clientdeploy");
            var bootstrapper = System.IO.Path.Combine(target, "bootstrapper.zip");
            Console.Out.WriteLine("Create folder structure " + cd);
            Directory.CreateDirectory(cd);

            Console.Out.WriteLine("Downloading ClientDeploy from "+ repo + "/clientdeploy.zip");
            using (var wc = new WebClient())
            {
                wc.DownloadFile(repo+"/clientdeploy.zip", bootstrapper);
            }

            Console.Out.WriteLine("Extracting ClientDeploy Installer");
            ZipFile.ExtractToDirectory(bootstrapper,cd);

            Console.Out.WriteLine("Removing temporary file");
            File.Delete(bootstrapper);

            var updater = System.IO.Directory.GetDirectories(Path.Combine(cd, ".updater"))
                .Select(_=>_.Split('\\').Last())
                .OrderByDescending(Semver).FirstOrDefault() ?? "";
            var cdup2 = Path.Combine(".clientdeploy", updater);

            Console.Out.WriteLine("Installing from "+cdup2);
            var psi = new ProcessStartInfo(
                cdup2,
                $"--install . --product {product} --repository {repo}") {WorkingDirectory = target};

            Process.Start(psi);
        }

        private static string Semver(string v)
        {
            return String.Join(".", v.Split('.').Select(_ => _.PadLeft(10, '0')));
        }

    }
}
