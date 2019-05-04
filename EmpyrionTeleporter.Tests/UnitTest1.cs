using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Eleon.Modding;
using EmpyrionNetAPITools;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EmpyrionTeleporter.Tests
{
    [TestClass]
    public class EmpyrionTeleporterUnitTest
    {
        [TestMethod]
        public void TestCommandLine()
        {
            string[] Args = "EmpyrionDedicated.exe -batchmode -nographics -dedicated dedicated_WEB.yaml -logFile Logs/1743/Dedicated_180714-091344-42.log".Split(' ');

            Assert.IsTrue(Args.Contains("-dedicated"));
            Assert.AreEqual("dedicated_WEB.yaml", Args.SkipWhile(A => string.Compare(A, "-dedicated", StringComparison.InvariantCultureIgnoreCase) != 0).Skip(1).FirstOrDefault());
        }

        [TestMethod]
        public void TestTeleportDB()
        {
            var G = new GlobalStructureList() { globalStructures = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<GlobalStructureInfo>>() };
            G.globalStructures.Add("Akua", (new GlobalStructureInfo[]{
                new GlobalStructureInfo() { factionId = 1, id=4004, pos = new PVector3(-182.306f, 71.67924f, 233.075f), rot= new PVector3(0, 4.48f, 0) },
                new GlobalStructureInfo() { factionId = 1, id=4005, pos = new PVector3(-1082.306f, 71.67924f, 2033.075f), rot= new PVector3(0, 34.48f, 0) },
            }).ToList());
            var P = new PlayerInfo() { factionId = 1, playfield = "Akua", pos = new PVector3(-1082.451f, 77.67921f, 2034.332f) };
            var P2 = new PlayerInfo() { factionId = 1, playfield = "Akua", pos = new PVector3(-182.451f, 77.67921f, 234.332f) };

            TeleporterDB db = new TeleporterDB("./TeleportDB.json");
            db.AddRoute(G, TeleporterPermission.PublicAccess, 4005, 4004, P);
            db.AddRoute(G, TeleporterPermission.PublicAccess, 4004, 4005, P2);

            db.Settings.Save();

            var Found = db.SearchRoute(G, P);
            Assert.IsNotNull(Found);
            Assert.IsTrue(Found.Id != 0);

            db.Settings.Load();
            Assert.AreEqual(1, db.Settings.Current.TeleporterRoutes.Count);
        }

        [TestMethod]
        public void TestDedicatedYaml()
        {
            var CurrentAssembly = Assembly.GetAssembly(typeof(EmpyrionTeleporter));
            var x = $"\n\n{CurrentAssembly.GetAttribute<AssemblyTitleAttribute>()?.Title} {CurrentAssembly.GetAttribute<AssemblyFileVersionAttribute>()?.Version} by {CurrentAssembly.GetAttribute<AssemblyCompanyAttribute>()?.Company}";

            var Dedi = new EmpyrionConfiguration.DedicatedYamlStruct(Path.Combine(EmpyrionConfiguration.ProgramPath, @"..\..\" + EmpyrionConfiguration.DedicatedFilename));

            Assert.AreEqual("DediGame",             Dedi.SaveGameName);
            Assert.AreEqual("Default Akua-Omicron", Dedi.CustomScenarioName);
        }

    }
}
