using EmpyrionAPIDefinitions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EmpyrionTeleporter
{
    public class AllowedStructure
    {
        public EntityType EntityType { get; set; }
        public FactionGroups FactionGroups { get; set; }
    }

    public class Configuration
    {
        public int HoldPlayerOnPositionAfterTeleport { get; set; } = 20;
        public int CostsPerTeleporterPosition { get; set; }
        public int CostsPerTeleport { get; set; }
        public AllowedStructure[] AllowedStructures { get; set; } = new AllowedStructure[] 
            {
                new AllowedStructure(){ EntityType = EntityType.BA, FactionGroups = FactionGroups.Player  },
                new AllowedStructure(){ EntityType = EntityType.BA, FactionGroups = FactionGroups.Faction },
                new AllowedStructure(){ EntityType = EntityType.CV, FactionGroups = FactionGroups.Player  },
                new AllowedStructure(){ EntityType = EntityType.CV, FactionGroups = FactionGroups.Faction },
            };
    }
}
