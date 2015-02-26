using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace TempleExtender
{
    public class Starter
    {
        /// <summary>
        /// Patches the loaded temple.dll library with the given settings.
        /// </summary>
        /// <param name="filename">Path to the temple.dll that should be used.</param>
        /// <param name="settings">The settings to use for starting the game.</param>
        /// <exception cref="Exception"></exception>
        public static void Start(string filename, Settings settings)
        {
            using (var library = new TempleLibrary(filename))
            {
                if (!library.Valid)
                {
                    throw new Exception(string.Format(
                        "Unable to start the game. Cannot load the game library {0}: {1}", filename, library.Error));
                }

                //var entries = library.ReadMemory(0x2EF14C, 40);
                //Console.WriteLine(BitConverter.ToString(entries));

                //entries[0] = 0xFF;
                //entries[entries.Length - 1] = 0xFF;

                //library.WriteMemory(0x2EF14C, entries);

                //entries = library.ReadMemory(0x2EF14C, 40);
                //Console.WriteLine(BitConverter.ToString(entries));

                using (new DebugOutputRedirection(library, settings))
                {
                    using (new FilesystemRedirector(library, settings))
                    {
                        library.StartGame(settings);
                    }
                }
            }
        }
    }
}