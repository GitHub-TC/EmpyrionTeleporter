using System;
using Eleon.Modding;
using EmpyrionAPITools;
using System.Collections.Generic;
using EmpyrionAPIDefinitions;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace EmpyrionTeleporter
{
    public static class Extensions{
        public static string String(this PVector3 aVector) => $"{aVector.x:F1},{aVector.y:F1},{aVector.z:F1}";
        public static string String(this Vector3 aVector) => $"{aVector.X:F1},{aVector.Y:F1},{aVector.Z:F1}";

        public static T GetAttribute<T>(this Assembly aAssembly)
        {
            return aAssembly.GetCustomAttributes(typeof(T), false).OfType<T>().FirstOrDefault();
        }

        static Regex GetCommand = new Regex(@"(?<cmd>(\w|\/|\s)+)");

        public static string MsgString(this ChatCommand aCommand)
        {
            var CmdString = GetCommand.Match(aCommand.invocationPattern).Groups["cmd"]?.Value ?? aCommand.invocationPattern;
            return $"[c][ff00ff]{CmdString}[-][/c]{aCommand.paramNames.Aggregate(" ", (S, P) => S + $"<[c][00ff00]{P}[-][/c]> ")}: {aCommand.description}";
        }

    }

    public partial class EmpyrionTeleporter : SimpleMod
    {
        public ModGameAPI GameAPI { get; set; }
        public int SourceId { get; private set; }
        public int TargetId { get; private set; }
        public Vector3 ShiftVector { get; private set; }
        public IdPositionRotation BaseToAlign { get; private set; }
        public IdPositionRotation MainBase { get; private set; }
        public bool WithinAlign { get; private set; }

        public GlobalStructureList GlobalStructureList { get; set; } = new GlobalStructureList() { globalStructures = new Dictionary<string, List<GlobalStructureInfo>>() };
        public TeleporterDB TeleporterDB { get; set; }
        public IdPlayfieldPositionRotation ExecOnLoadedPlayfield { get; private set; }

        Dictionary<int, IdPlayfieldPositionRotation> PlayerLastGoodPosition = new Dictionary<int, IdPlayfieldPositionRotation>();

        public string TeleporterDBFilename { get; set; }

        FileSystemWatcher DBFileChangedWatcher;

        enum SubCommand
        {
            Help,
            Back,
            Delete,
            List,
            ListAll,
            CleanUp,
            Save,
            Teleport
        }

        public override void Initialize(ModGameAPI aGameAPI)
        {
            GameAPI = aGameAPI;
            verbose = true;

            log($"**HandleEmpyrionTeleporter loaded: {string.Join(" ", Environment.GetCommandLineArgs())}");

            InitializeTeleporterDB();
            InitializeTeleporterDBFileWatcher();

            ChatCommands.Add(new ChatCommand(@"/tt",                                            (I, A) => ExecAlignCommand(SubCommand.Teleport, TeleporterPermission.PublicAccess,  I, A), "Execute teleport"));
            ChatCommands.Add(new ChatCommand(@"/tt help",                                       (I, A) => ExecAlignCommand(SubCommand.Help,     TeleporterPermission.PublicAccess,  I, A), "Display help"));
            ChatCommands.Add(new ChatCommand(@"/tt back",                                       (I, A) => ExecAlignCommand(SubCommand.Back,     TeleporterPermission.PublicAccess,  I, A), "Teleports the player back to the last (good) position"));
            ChatCommands.Add(new ChatCommand(@"/tt delete (?<Id>\d+)",                          (I, A) => ExecAlignCommand(SubCommand.Delete,   TeleporterPermission.PublicAccess,  I, A), "Delete all teleportdata from {Id}"));
            ChatCommands.Add(new ChatCommand(@"/tt list (?<Id>\d+)",                            (I, A) => ExecAlignCommand(SubCommand.List,     TeleporterPermission.PublicAccess,  I, A), "List all teleportdata from {Id}"));
            ChatCommands.Add(new ChatCommand(@"/tt listall",                                    (I, A) => ExecAlignCommand(SubCommand.ListAll,  TeleporterPermission.PublicAccess,  I, A), "List all teleportdata", PermissionType.Moderator));
            ChatCommands.Add(new ChatCommand(@"/tt cleanup",                                    (I, A) => ExecAlignCommand(SubCommand.CleanUp,  TeleporterPermission.PublicAccess,  I, A), "Removes all teleportdata to deleted structures", PermissionType.Moderator));
            ChatCommands.Add(new ChatCommand(@"/tt private (?<SourceId>\d+) (?<TargetId>\d+)",  (I, A) => ExecAlignCommand(SubCommand.Save,     TeleporterPermission.PrivateAccess, I, A), "Init Teleport from {SourceId} (PlayerPosition) to {TargetId} accessible is allowed for you only - must be initialized at {TargetId} too :-)"));
            ChatCommands.Add(new ChatCommand(@"/tt faction (?<SourceId>\d+) (?<TargetId>\d+)",  (I, A) => ExecAlignCommand(SubCommand.Save,     TeleporterPermission.FactionAccess, I, A), "Init Teleport from {SourceId} (PlayerPosition) to {TargetId} accessible is allowed for your faction - must be initialized at {TargetId} too :-)"));
            ChatCommands.Add(new ChatCommand(@"/tt (?<SourceId>\d+) (?<TargetId>\d+)",          (I, A) => ExecAlignCommand(SubCommand.Save,     TeleporterPermission.PublicAccess,  I, A), "Init Teleport from {SourceId} (PlayerPosition) to {TargetId} accessible is allowed for everyone - must be initialized at {TargetId} too :-)"));
        }

        private void InitializeTeleporterDBFileWatcher()
        {
            DBFileChangedWatcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(TeleporterDBFilename),
                NotifyFilter = NotifyFilters.LastWrite,
                Filter = Path.GetFileName(TeleporterDBFilename)
            };
            DBFileChangedWatcher.Changed += (s, e) => TeleporterDB = TeleporterDB.ReadDB(TeleporterDBFilename);
            DBFileChangedWatcher.EnableRaisingEvents = true;
        }

        private void InitializeTeleporterDB()
        {
            TeleporterDBFilename = Path.Combine(EmpyrionConfiguration.ProgramPath, @"Saves\Games\" + EmpyrionConfiguration.DedicatedYaml.SaveGameName + @"\Mods\EmpyrionTeleporter\TeleporterDB.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(TeleporterDBFilename));

            // Move DB file to new location
            var OldDB = Path.Combine(Directory.GetCurrentDirectory(), @"Content\Mods\EmpyrionTeleporter\TeleporterDB.xml");
            if (File.Exists(OldDB)) File.Move(OldDB, TeleporterDBFilename);

            TeleporterDB.LogDB = log;
            TeleporterDB = TeleporterDB.ReadDB(TeleporterDBFilename);
            TeleporterDB.SaveDB(TeleporterDBFilename);
        }


        enum ChatType
        {
            Global = 3,
            Faction = 5,
        }

        private void ExecAlignCommand(SubCommand aCommand, TeleporterPermission aPermission, ChatInfo info, Dictionary<string, string> args)
        {
            log($"**HandleEmpyrionTeleporter {info.type}:{info.msg} {args.Aggregate("", (s, i) => s + i.Key + "/" + i.Value + " ")}");

            if (info.type != (byte)ChatType.Faction) return;

            switch (aCommand)
            {
                case SubCommand.Help    : DisplayHelp(info.playerId); break;
                case SubCommand.Back    : ExecTeleportPlayerBack(info.playerId); break;
                case SubCommand.Delete  : DeleteTeleporterRoutes(info.playerId, getIntParam(args, "Id")); break;
                case SubCommand.List    : ListTeleporterRoutes(info.playerId, getIntParam(args, "Id")); break;
                case SubCommand.ListAll : ListAllTeleporterRoutes(info.playerId); break;
                case SubCommand.CleanUp : CleanUpTeleporterRoutes(info.playerId); break;
                case SubCommand.Save    : SaveTeleporterRoute(info.playerId, aPermission, getIntParam(args, "SourceId"), getIntParam(args, "TargetId")); break;
                case SubCommand.Teleport: TeleportPlayer(info.playerId); break;
            }
        }

        private void CleanUpTeleporterRoutes(int aPlayerId)
        {
            Request_GlobalStructure_List(G => {
                var GlobalFlatIdList = G.globalStructures.Aggregate(new List<int>(), (L, P) => { L.AddRange(P.Value.Select(S => S.id)); return L; });
                var TeleporterFlatIdList = TeleporterDB.TeleporterRoutes.Aggregate(new List<int>(), (L, P) => { L.Add(P.A.Id); L.Add(P.B.Id); return L; });

                var DeleteList = TeleporterFlatIdList.Where(I => !GlobalFlatIdList.Contains(I)).Distinct();
                var DelCount = DeleteList.Aggregate(0, (C, I) => C + TeleporterDB.Delete(I));
                log($"CleanUpTeleporterRoutes: {DelCount} Structures: {DeleteList.Aggregate("", (S, I) => S + "," + I)}");
                InformPlayer(aPlayerId, $"CleanUp: {DelCount} TeleporterRoutes");

                if (DelCount > 0) SaveTeleporterDB();
            });
        }

        private void TeleportPlayer(int aPlayerId)
        {
            Request_GlobalStructure_List(G =>
                Request_Player_Info(aPlayerId.ToId(), P => {
                    if (P.credits < TeleporterDB.Configuration.CostsPerTeleport) AlertPlayer(P.entityId, $"You need {TeleporterDB.Configuration.CostsPerTeleport} credits ;-)");
                    else if (ExecTeleportPlayer(G, P, aPlayerId) && TeleporterDB.Configuration.CostsPerTeleport > 0) Request_Player_SetCredits(new IdCredits(P.entityId, P.credits - TeleporterDB.Configuration.CostsPerTeleport));
                }));
        }

        private void SaveTeleporterRoute(int aPlayerId, TeleporterPermission aPermission, int aSourceId, int aTargetId)
        {
            Request_GlobalStructure_List(G =>
            {
                Request_Player_Info(aPlayerId.ToId(), (P) =>
                {
                    var SourceStructure = TeleporterDB.SearchEntity(G, aSourceId);
                    var TargetStructure = TeleporterDB.SearchEntity(G, aSourceId);

                    if (SourceStructure == null) AlertPlayer(P.entityId, $"Structure not found: {aSourceId}");
                    else if (TargetStructure == null) AlertPlayer(P.entityId, $"Structure not found: {aTargetId}");
                    else if (!CheckPermission(SourceStructure, TeleporterDB.Configuration)) AlertPlayer(P.entityId, $"Structure not allowed: {aSourceId} {(EntityType)SourceStructure.Data.type}/{(FactionGroups)SourceStructure.Data.factionGroup}");
                    else if (!CheckPermission(TargetStructure, TeleporterDB.Configuration)) AlertPlayer(P.entityId, $"Structure not allowed: {aTargetId} {(EntityType)TargetStructure.Data.type}/{(FactionGroups)TargetStructure.Data.factionGroup}");
                    else if (P.credits < TeleporterDB.Configuration.CostsPerTeleporterPosition) AlertPlayer(P.entityId, $"You need {TeleporterDB.Configuration.CostsPerTeleporterPosition} credits ;-)");
                    else
                    {
                        TeleporterDB.AddRoute(G, aPermission, aSourceId, aTargetId, P);
                        SaveTeleporterDB();

                        if (TeleporterDB.Configuration.CostsPerTeleporterPosition > 0) Request_Player_SetCredits(new IdCredits(P.entityId, P.credits - TeleporterDB.Configuration.CostsPerTeleporterPosition));

                        ShowDialog(aPlayerId, P, "Teleporters", $"\nTeleporter set\n{aSourceId} => {aTargetId}");
                    }
                });
            });
        }

        private void SaveTeleporterDB()
        {
            DBFileChangedWatcher.EnableRaisingEvents = false;
            TeleporterDB.SaveDB(TeleporterDBFilename);
            DBFileChangedWatcher.EnableRaisingEvents = true;
        }

        private bool CheckPermission(TeleporterDB.PlayfieldStructureInfo aStructure, Configuration aConfiguration)
        {
            return aConfiguration.AllowedStructures.Any(C =>
                C.EntityType == (EntityType)aStructure.Data.type &&
                C.FactionGroups == (FactionGroups)aStructure.Data.factionGroup
            );
        }

        private void ListAllTeleporterRoutes(int aPlayerId)
        {
            Request_Player_Info(aPlayerId.ToId(), (P) =>
            {
                ShowDialog(aPlayerId, P, "Teleporters", TeleporterDB.TeleporterRoutes.OrderBy(T => T.Permission).Aggregate("\n", (S, T) => S + T.ToString() + "\n"));
            });
        }

        private void DeleteTeleporterRoutes(int aPlayerId, int aStructureId)
        {
            Request_Player_Info(aPlayerId.ToId(), (P) =>
            {
                var deletedCount = TeleporterDB.Delete(aStructureId);
                SaveTeleporterDB();

                AlertPlayer(P.entityId, $"Delete {deletedCount} teleporter from {aStructureId}");
            });
        }

        private void ListTeleporterRoutes(int aPlayerId, int aStructureId)
        {
            Request_Player_Info(aPlayerId.ToId(), (P) =>
            {
                ShowDialog(aPlayerId, P, "Teleporters", TeleporterDB.List(aStructureId, P).OrderBy(T => T.Permission).Aggregate("\n", (S, T) => S + T.ToString() + "\n"));
            });
        }

        private bool ExecTeleportPlayer(GlobalStructureList aGlobalStructureList, PlayerInfo aPlayer, int aPlayerId)
        {
            var FoundRoute = TeleporterDB.SearchRoute(aGlobalStructureList, aPlayer);
            if (FoundRoute == null)
            {
                InformPlayer(aPlayer.entityId, "No teleporter position here :-( wait 2min for structure update and try it again please.");
                log($"EmpyrionTeleporter: Exec: {aPlayer.playerName}/{aPlayer.entityId}/{aPlayer.clientId} -> no route found for pos={GetVector3(aPlayer.pos).String()} on '{aPlayer.playfield}'");
                return false;
            }

            log($"EmpyrionTeleporter: Exec: {aPlayer.playerName}/{aPlayer.entityId}-> {FoundRoute.Id} on '{FoundRoute.Playfield}' pos={FoundRoute.Position.String()} rot={FoundRoute.Rotation.String()}");

            if (!PlayerLastGoodPosition.ContainsKey(aPlayer.entityId)) PlayerLastGoodPosition.Add(aPlayer.entityId, null);
            PlayerLastGoodPosition[aPlayer.entityId] = new IdPlayfieldPositionRotation(aPlayer.entityId, aPlayer.playfield, aPlayer.pos, aPlayer.rot);

            Action ActionTeleportPlayer = () =>
            {
                if (FoundRoute.Playfield == aPlayer.playfield) Request_Entity_Teleport(new IdPositionRotation(aPlayer.entityId, GetVector3(FoundRoute.Position), GetVector3(FoundRoute.Rotation)));
                else Request_Player_ChangePlayerfield(new IdPlayfieldPositionRotation(aPlayer.entityId, FoundRoute.Playfield, GetVector3(FoundRoute.Position), GetVector3(FoundRoute.Rotation)));
            };

            ActionTeleportPlayer();
            CheckPlayerStableTargetPos(aPlayerId, ActionTeleportPlayer, FoundRoute.Position);

            return true;
        }

        private void CheckPlayerStableTargetPos(int aPlayerId, Action ActionTeleportPlayer, Vector3 aTargetPos)
        {
            new Thread(new ThreadStart(() =>
            {
                var TryTimer = new Stopwatch();
                TryTimer.Start();
                while (TryTimer.ElapsedMilliseconds < (TeleporterDB.Configuration.HoldPlayerOnPositionAfterTeleport * 1000))
                {
                    Thread.Sleep(1000);
                    var WaitTime = TeleporterDB.Configuration.HoldPlayerOnPositionAfterTeleport - (int)(TryTimer.ElapsedMilliseconds / 1000);
                    Request_Player_Info(aPlayerId.ToId(), P => {
                        if (Vector3.Distance(GetVector3(P.pos), aTargetPos) > 3) ActionTeleportPlayer();
                        else if (WaitTime > 0) InformPlayer(aPlayerId, $"Target reached please wait for {WaitTime} sec.");
                    }, (E) => InformPlayer(aPlayerId, "Target reached. {E}"));
                }
                InformPlayer(aPlayerId, $"Thank you for traveling with the EmpyrionTeleporter :-)");
            })).Start();
        }

        private void ExecTeleportPlayerBack(int aPlayerId)
        {
            Request_Player_Info(aPlayerId.ToId(), P => {

                if (!PlayerLastGoodPosition.ContainsKey(P.entityId))
                {
                    InformPlayer(aPlayerId, "No back teleport available.");
                    return;
                }

                var LastGoodPos = PlayerLastGoodPosition[P.entityId];
                PlayerLastGoodPosition.Remove(P.entityId);

                if (LastGoodPos.playfield == P.playfield) Request_Entity_Teleport(new IdPositionRotation(P.entityId, LastGoodPos.pos, LastGoodPos.rot));
                else Request_Player_ChangePlayerfield(LastGoodPos);
            });
        }

        public static Vector3 GetVector3(PVector3 aVector)
        {
            return new Vector3(aVector.x, aVector.y, aVector.z);
        }

        public static PVector3 GetVector3(Vector3 aVector)
        {
            return new PVector3(aVector.X, aVector.Y, aVector.Z);
        }

        private void LogError(string aPrefix, ErrorInfo aError)
        {
            log($"{aPrefix} Error: {aError.errorType} {aError.ToString()}");
        }

        private int getIntParam(Dictionary<string, string> aArgs, string aParameterName)
        {
            string valueStr;
            if (!aArgs.TryGetValue(aParameterName, out valueStr)) return 0;

            int value;
            if (!int.TryParse(valueStr, out value)) return 0;

            return value;
        }

        void ShowDialog(int aPlayerId, PlayerInfo aPlayer, string aTitle, string aMessage)
        {
            Request_ShowDialog_SinglePlayer(new DialogBoxData()
            {
                Id      = aPlayerId,
                MsgText = $"{aTitle}: [c][ffffff]{aPlayer.playerName}[-][/c] with permission [c][ffffff]{(PermissionType)aPlayer.permission}[-][/c]\n" + aMessage,
            });
        }

        private void DisplayHelp(int aPlayerId)
        {
            Request_Player_Info(aPlayerId.ToId(), (P) =>
            {
                var CurrentAssembly = Assembly.GetAssembly(this.GetType());
                //[c][hexid][-][/c]    [c][019245]test[-][/c].

                ShowDialog(aPlayerId, P, "Commands",
                    "\n" + String.Join("\n", GetChatCommandsForPermissionLevel((PermissionType)P.permission).Select(C => C.MsgString()).ToArray()) +
                    $"\n\nCosts teleporter set: [c][ff0000]{TeleporterDB.Configuration.CostsPerTeleporterPosition}[-][/c] credits" +
                    $"\nCosts teleporter use: [c][ff0000]{TeleporterDB.Configuration.CostsPerTeleport}[-][/c] credits" +
                    TeleporterDB.Configuration.AllowedStructures.Aggregate("\n\n[c][00ffff]Teleporter allowed at:[-][/c]", (s, a) => s + $"\n {a.EntityType}/{a.FactionGroups}") +
                    $"\n\n[c][c0c0c0]{CurrentAssembly.GetAttribute<AssemblyTitleAttribute>()?.Title} by {CurrentAssembly.GetAttribute<AssemblyCompanyAttribute>()?.Company} Version:{CurrentAssembly.GetAttribute<AssemblyFileVersionAttribute>()?.Version}[-][/c]"
                    );
            });
        }

    }
}
