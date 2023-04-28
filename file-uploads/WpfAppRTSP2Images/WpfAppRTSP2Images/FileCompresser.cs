using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfAppRTSP2Images
{
    public class FileCompresser
    {
        public string ZipFilePath { get; set; }
        public long FileSize
        {
            get
            {
                var fi = new FileInfo(ZipFilePath);
                return fi.Length;
            }
        }

        public void CreateZipFile()
        {
            using (var fs = File.Create(ZipFilePath))
            {

            }
        }

        public void AddFile(string filePath)
        {
            using (var fs = new FileStream(ZipFilePath, FileMode.Open))
            {
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Update))
                {
                    var fi = new FileInfo(filePath);
                    var newEntry = zip.CreateEntry(filePath);
                    zip.CreateEntryFromFile(filePath, fi.Name);
                }
            }
        }
    }
}
