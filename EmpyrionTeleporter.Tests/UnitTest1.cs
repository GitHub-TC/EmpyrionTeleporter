using System;
using System.Linq;
using Eleon.Modding;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EmpyrionTeleporter.Tests
{
    [TestClass]
    public class EmpyrionTeleporterUnitTest
    {
        [TestMethod]
        public void TestMethod1()
        {
            var G = new GlobalStructureList() { globalStructures = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<GlobalStructureInfo>>() };
            G.globalStructures.Add("Akua", (new GlobalStructureInfo[]{
                new GlobalStructureInfo() { factionId = 1, id=4004, pos = new PVector3(-182.306f, 71.67924f, 233.075f), rot= new PVector3(0, 4.48f, 0) },
                new GlobalStructureInfo() { factionId = 1, id=4005, pos = new PVector3(-1082.306f, 71.67924f, 2033.075f), rot= new PVector3(0, 34.48f, 0) },
            }).ToList() );
            var P = new PlayerInfo() { factionId = 1, playfield = "Akua", pos = new PVector3(-1082.451f, 77.67921f, 2034.332f) };
            var P2 = new PlayerInfo() { factionId = 1, playfield = "Akua", pos = new PVector3(-182.451f, 77.67921f, 234.332f) };

            TeleporterDB db = new TeleporterDB();
            db.AddRoute(G, TeleporterPermission.PublicAccess, 4005, 4004, P);
            db.AddRoute(G, TeleporterPermission.PublicAccess, 4004, 4005, P2);

            db.SaveDB();

            var Found = db.SearchRoute(G, P);
            Assert.IsNotNull(Found);
            Assert.IsTrue(Found.Id != 0);

            var Test = TeleporterDB.ReadDB();
            Assert.AreEqual(1, Test.TeleporterRoutes.Count);
        }
    }
}
