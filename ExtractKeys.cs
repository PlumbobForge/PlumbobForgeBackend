using System;
using System.IO;
using S3ForgeTools.GameFiles.Package;
using S3ForgeTools.GameFiles.TS3Pack;

class Program {
    static void Main(string[] args) {
        string path = args[0];
        if (path.EndsWith("package")) {
            using var pkg = new DBPFPackage(path);
            foreach (var res in pkg.Resources) {
                Console.WriteLine(res.Key.Type.ToString("X8") + "-" + res.Key.Group.ToString("X8") + "-" + res.Key.Instance.ToString("X16") + " Size: " + res.Length);
            }
        }
    }
}
