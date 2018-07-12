using EmpyrionAPIDefinitions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EmpyrionTeleporter
{
    public class AllowedStructure
    {
        public CoreType CoreType { get; set; }
        public EntityType EntityType { get; set; }
        public FactionGroups FactionGroups { get; set; }
    }

    public class Configuration
    {
        public int CostsPerTeleporterPosition { get; set; }
        public int CostsPerTeleport { get; set; }
        public List<AllowedStructure> AllowedStructures { get; set; } = new AllowedStructure[] 
            {
                new AllowedStructure(){ CoreType = CoreType.Player_Core, EntityType = EntityType.BA, FactionGroups = FactionGroups.Player },
                new AllowedStructure(){ CoreType = CoreType.Player_Core, EntityType = EntityType.CV, FactionGroups = FactionGroups.Player },
            }.ToList();
    }
}
