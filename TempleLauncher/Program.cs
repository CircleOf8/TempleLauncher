using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using TempleExtender;
using System.Windows.Forms;

namespace TempleStarter
{
    class Program
    {

        private string _dllPath;

        private Settings _settings;

        void Start()
        {
            var d = new OpenFileDialog();
            d.Filter = "Module (*.tfm)|*.tfm";
            d.ShowDialog();
            _settings.DataArchives.Add(d.FileName);
        }

        static void Main(string[] args)
        {
            const string path = @"E:\Temple of Elemental Evil\";
            const string dll = "temple.dll";
            var dllPath = Path.Combine(path, dll);

            var settings = new Settings();
            settings.Windowed = true;
            settings.DisableIntroMovies = true;
            settings.InstallationDirectory = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
            settings.DebuggingOutput = true;
            settings.MountedDirectories.Add(@"modules\toee\save",
                                            @"C:\Users\<user>\Documents\Temple of Elemental Evil\Save Games");
            settings.MountedDirectories.Add(@"toee.cfg",
                                            @"C:\Users\<user>\Documents\Temple of Elemental Evil\config.ini");
            settings.MountedDirectories.Add(@"debug.txt",
                                            @"C:\Users\<user>\Documents\Temple of Elemental Evil\debug.txt");

            // settings.DataArchives.Add(Path.Combine(settings.InstallationDirectory, "co8.dat"));

            var writeDirectory = Path.Combine(settings.InstallationDirectory,
                                              @"Circle of Eight Modpack 5.9.2 BETA - OUT");

            settings.VirtualWriteDirectory = writeDirectory;
            settings.WriteAccessVirtualization = false;
            settings.DirectoryOverlays.Add(writeDirectory);

            /*settings.DirectoryOverlays.Add(Path.Combine(settings.InstallationDirectory,
                                           @"Circle of Eight Modpack 5.9.2 BETA"));*/

            Starter.Start(dllPath, settings);
        }

    }
}
