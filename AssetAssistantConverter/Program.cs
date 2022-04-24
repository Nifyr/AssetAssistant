using Newtonsoft.Json;
using SmartPoint.AssetAssistant;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Example
{
    static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            OpenFileDialog ofd = new()
            {
                Filter = "AssetAssistant Objects(*.bin;*.json)|*.bin;*.json",
                RestoreDirectory = true
            };

            if (ofd.ShowDialog() != DialogResult.OK) return;

            string outputPath = Environment.CurrentDirectory + "\\Output";

            if (Path.GetExtension(ofd.FileName) == ".bin")
            {
                string destinationFileName = Environment.CurrentDirectory + "\\Output\\" + Path.GetFileNameWithoutExtension(ofd.FileName) + ".json";
                string jsonString = JsonConvert.SerializeObject(AssetBundleDownloadManifest.Load(ofd.FileName), Formatting.Indented);
                Directory.CreateDirectory(outputPath);
                File.WriteAllText(destinationFileName, jsonString);
            }
            else if (Path.GetExtension(ofd.FileName) == ".json")
            {
                string destinationFileName = Environment.CurrentDirectory + "\\Output\\" + Path.GetFileNameWithoutExtension(ofd.FileName) + ".bin";
                string jsonString = File.ReadAllText(ofd.FileName);
                AssetBundleDownloadManifest abdm = JsonConvert.DeserializeObject<AssetBundleDownloadManifest>(jsonString);
                Directory.CreateDirectory(outputPath);
                abdm.Save(destinationFileName);
            }
        }
    }
}
