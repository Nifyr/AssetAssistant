using Newtonsoft.Json;
using SmartPoint.AssetAssistant;
using System;
using System.IO;
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

            string outputDir = Environment.CurrentDirectory + "\\Output";

            if (Path.GetExtension(ofd.FileName) == ".bin")
            {
                try
                {
                    string destFileName = outputDir + "\\" + Path.GetFileNameWithoutExtension(ofd.FileName) + ".json";
                    string json = JsonConvert.SerializeObject(AssetBundleDownloadManifest.Load(ofd.FileName), Formatting.Indented);
                    Directory.CreateDirectory(outputDir);
                    File.WriteAllText(destFileName, json);
                    MessageBox.Show("Json placed in Output folder.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else if (Path.GetExtension(ofd.FileName) == ".json")
            {
                try
                {
                    string destFileName = outputDir + "\\" + Path.GetFileNameWithoutExtension(ofd.FileName) + ".bin";
                    string json = File.ReadAllText(ofd.FileName);
                    AssetBundleDownloadManifest abdm = JsonConvert.DeserializeObject<AssetBundleDownloadManifest>(json);
                    Directory.CreateDirectory(outputDir);
                    abdm.Save(destFileName);
                    MessageBox.Show("AssetAssistant-object placed in Output folder.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
