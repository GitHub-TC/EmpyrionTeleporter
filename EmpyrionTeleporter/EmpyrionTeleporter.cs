using Eleon.Modding;
using EmpyrionNetAPIAccess;
using EmpyrionNetAPIDefinitions;
using EmpyrionNetAPITools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

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

    public class EmpyrionTeleporter : EmpyrionModBase
    {
        public ModGameAPI GameAPI { get; set; }
        public TeleporterDB TeleporterDB { get; set; }

        Dictionary<int, IdPlayfieldPositionRotation> PlayerLastGoodPosition = new Dictionary<int, IdPlayfieldPositionRotation>();

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

        public EmpyrionTeleporter()
        {
            EmpyrionConfiguration.ModName = "EmpyrionTeleporter";
        }

        public override void Initialize(ModGameAPI aGameAPI)
        {
            GameAPI = aGameAPI;
            verbose = true;
            LogLevel = LogLevel.Message;

            log($"**HandleEmpyrionTeleporter loaded: {string.Join(" ", Environment.GetCommandLineArgs())}", LogLevel.Message);

            InitializeTeleporterDB();

            ChatCommands.Add(new ChatCommand(@"/tt",                                            (I, A) => ExecAlignCommand(SubCommand.Teleport, TeleporterPermission.PublicAccess,  I, A), "Execute teleport"));
            ChatCommands.Add(new ChatCommand(@"/tt help",                                       (I, A) => ExecAlignCommand(SubCommand.Help,     TeleporterPermission.PublicAccess,  I, A), "Display help"));
            ChatCommands.Add(new ChatCommand(@"/tt back",                                       (I, A) => ExecAlignCommand(SubCommand.Back,     TeleporterPermission.PublicAccess,  I, A), "Teleports the player back to the last (good) position"));
            ChatCommands.Add(new ChatCommand(@"/tt delete (?<SourceId>\d+) (?<TargetId>\d+)",   (I, A) => ExecAlignCommand(SubCommand.Delete,   TeleporterPermission.PublicAccess,  I, A), "Delete all teleportdata from {SourceId} {TargetId}"));
            ChatCommands.Add(new ChatCommand(@"/tt list (?<Id>\d+)",                            (I, A) => ExecAlignCommand(SubCommand.List,     TeleporterPermission.PublicAccess,  I, A), "List all teleportdata from {Id}"));
            ChatCommands.Add(new ChatCommand(@"/tt listall",                                    (I, A) => ExecAlignCommand(SubCommand.ListAll,  TeleporterPermission.PublicAccess,  I, A), "List all teleportdata", PermissionType.Moderator));
            ChatCommands.Add(new ChatCommand(@"/tt cleanup",                                    (I, A) => ExecAlignCommand(SubCommand.CleanUp,  TeleporterPermission.PublicAccess,  I, A), "Removes all teleportdata to deleted structures", PermissionType.Moderator));
            ChatCommands.Add(new ChatCommand(@"/tt private (?<SourceId>\d+) (?<TargetId>\d+)",  (I, A) => ExecAlignCommand(SubCommand.Save,     TeleporterPermission.PrivateAccess, I, A), "Init Teleport from {SourceId} (PlayerPosition) to {TargetId} accessible is allowed for you only - must be initialized at {TargetId} too :-)"));
            ChatCommands.Add(new ChatCommand(@"/tt faction (?<SourceId>\d+) (?<TargetId>\d+)",  (I, A) => ExecAlignCommand(SubCommand.Save,     TeleporterPermission.FactionAccess, I, A), "Init Teleport from {SourceId} (PlayerPosition) to {TargetId} accessible is allowed for your faction - must be initialized at {TargetId} too :-)"));
            ChatCommands.Add(new ChatCommand(@"/tt allies (?<SourceId>\d+) (?<TargetId>\d+)",   (I, A) => ExecAlignCommand(SubCommand.Save,     TeleporterPermission.AlliesAccess,  I, A), "Init Teleport from {SourceId} (PlayerPosition) to {TargetId} accessible is allowed for your faction and allies - must be initialized at {TargetId} too :-)"));
            ChatCommands.Add(new ChatCommand(@"/tt (?<SourceId>\d+) (?<TargetId>\d+)",          (I, A) => ExecAlignCommand(SubCommand.Save,     TeleporterPermission.PublicAccess,  I, A), "Init Teleport from {SourceId} (PlayerPosition) to {TargetId} accessible is allowed for everyone - must be initialized at {TargetId} too :-)"));
        }

        private void InitializeTeleporterDB()
        {
            TeleporterDB.LogDB = log;
            TeleporterDB = new TeleporterDB(Path.Combine(EmpyrionConfiguration.SaveGameModPath, "Teleporters.json"))
            {
                AreAllies = AreAllies
            };
        }

        private async Task<bool> AreAllies(int fractionId, int testFactionId)
        {
            if (fractionId == testFactionId) return true;

            var allFactions = (await Request_Get_Factions(new Id(1))).factions;
            var f1 = allFactions.FirstOrDefault(F => F.factionId == fractionId);
            var f2 = allFactions.FirstOrDefault(F => F.factionId == testFactionId);

            var allied = f1.origin == f2.origin;  // default allied

            var allies = await Request_AlliancesAll();
            var allyTest1 = f1.factionId << 16 | f2.factionId;
            var allyTest2 = f2.factionId << 16 | f1.factionId;

            if (allies.alliances != null && (allies.alliances.Contains(allyTest1) || allies.alliances.Contains(allyTest2))) allied = !allied; // default changed

            return allied;
        }

        enum ChatType
        {
            Global = 3,
            Faction = 5,
        }

        private async Task ExecAlignCommand(SubCommand aCommand, TeleporterPermission aPermission, ChatInfo info, Dictionary<string, string> args)
        {
            log($"**HandleEmpyrionTeleporter {info.type}:{info.msg} {args.Aggregate("", (s, i) => s + i.Key + "/" + i.Value + " ")}", LogLevel.Message);

            if (info.type != (byte)ChatType.Faction) return;

            switch (aCommand)
            {
                case SubCommand.Help    :       DisplayHelp             (info.playerId); break;
                case SubCommand.Back    : await ExecTeleportPlayerBack  (info.playerId); break;
                case SubCommand.Delete  : await DeleteTeleporterRoutes  (info.playerId, getIntParam(args, "SourceId"), getIntParam(args, "TargetId")); break;
                case SubCommand.List    : await ListTeleporterRoutes    (info.playerId, getIntParam(args, "Id")); break;
                case SubCommand.ListAll : await ListAllTeleporterRoutes (info.playerId); break;
                case SubCommand.CleanUp : await CleanUpTeleporterRoutes (info.playerId); break;
                case SubCommand.Save    : await SaveTeleporterRoute     (info.playerId, aPermission, getIntParam(args, "SourceId"), getIntParam(args, "TargetId")); break;
                case SubCommand.Teleport: await TeleportPlayer          (info.playerId); break;
            }
        }

        private async Task CleanUpTeleporterRoutes(int aPlayerId)
        {
            var G = await Request_GlobalStructure_List();

            var GlobalFlatIdList = G.globalStructures.Aggregate(new List<int>(), (L, P) => { L.AddRange(P.Value.Select(S => S.id)); return L; });
            var TeleporterFlatIdList = TeleporterDB.Settings.Current.TeleporterRoutes.Aggregate(new List<int>(), (L, P) => { L.Add(P.A.Id); L.Add(P.B.Id); return L; });

            var DeleteList = TeleporterFlatIdList.Where(I => !GlobalFlatIdList.Contains(I)).Distinct();
            var DelCount = DeleteList.Aggregate(0, (C, I) => C + TeleporterDB.Delete(I, 0));
            log($"CleanUpTeleporterRoutes: {DelCount} Structures: {DeleteList.Aggregate("", (S, I) => S + "," + I)}", LogLevel.Message);
            InformPlayer(aPlayerId, $"CleanUp: {DelCount} TeleporterRoutes");

            if (DelCount > 0) TeleporterDB.Settings.Save();
        }

        private async Task TeleportPlayer(int aPlayerId)
        {
            var G = await Request_GlobalStructure_List();
            var P = await Request_Player_Info(aPlayerId.ToId());

            if (P.credits < TeleporterDB.Settings.Current.CostsPerTeleport) AlertPlayer(P.entityId, $"You need {TeleporterDB.Settings.Current.CostsPerTeleport} credits ;-)");
            else if (await ExecTeleportPlayer(G, P, aPlayerId) && TeleporterDB.Settings.Current.CostsPerTeleport > 0) await Request_Player_SetCredits(new IdCredits(P.entityId, P.credits - TeleporterDB.Settings.Current.CostsPerTeleport));
        }

        private async Task SaveTeleporterRoute(int aPlayerId, TeleporterPermission aPermission, int aSourceId, int aTargetId)
        {
            var G = await Request_GlobalStructure_List();
            var P = await Request_Player_Info(aPlayerId.ToId());

            var SourceStructure = TeleporterDB.SearchEntity(G, aSourceId);
            var TargetStructure = TeleporterDB.SearchEntity(G, aSourceId);

            if (SourceStructure == null) AlertPlayer(P.entityId, $"Structure not found: {aSourceId}");
            else if (TargetStructure == null) AlertPlayer(P.entityId, $"Structure not found: {aTargetId}");
            else if (!CheckPermission(SourceStructure, TeleporterDB.Settings.Current)) AlertPlayer(P.entityId, $"Structure not allowed: {aSourceId} {(EntityType)SourceStructure.Data.type}/{(FactionGroups)SourceStructure.Data.factionGroup}");
            else if (!CheckPermission(TargetStructure, TeleporterDB.Settings.Current)) AlertPlayer(P.entityId, $"Structure not allowed: {aTargetId} {(EntityType)TargetStructure.Data.type}/{(FactionGroups)TargetStructure.Data.factionGroup}");
            else if (P.credits < TeleporterDB.Settings.Current.CostsPerTeleporterPosition) AlertPlayer(P.entityId, $"You need {TeleporterDB.Settings.Current.CostsPerTeleporterPosition} credits ;-)");
            else if (TeleporterDB.Settings.Current.ForbiddenPlayfields.Contains(P.playfield)               ||
                        TeleporterDB.Settings.Current.ForbiddenPlayfields.Contains(SourceStructure.Playfield) ||
                        TeleporterDB.Settings.Current.ForbiddenPlayfields.Contains(TargetStructure.Playfield))
            {
                InformPlayer(aPlayerId, "No teleporter allowed here ;-)");
                log($"EmpyrionTeleporter: Exec: {P.playerName}/{P.entityId}/{P.clientId} -> no teleport allowed for pos={GetVector3(P.pos).String()} on '{P.playfield}'", LogLevel.Error);
            }
            else
            {
                TeleporterDB.AddRoute(G, aPermission, aSourceId, aTargetId, P);
                TeleporterDB.Settings.Save();

                if (TeleporterDB.Settings.Current.CostsPerTeleporterPosition > 0) await Request_Player_SetCredits(new IdCredits(P.entityId, P.credits - TeleporterDB.Settings.Current.CostsPerTeleporterPosition));

                await ShowDialog(aPlayerId, P, "Teleporters", $"\nTeleporter set\n{aSourceId} => {aTargetId}");
            }
        }

        private bool CheckPermission(TeleporterDB.PlayfieldStructureInfo aStructure, TeleporterDB.ConfigurationAndDB aConfiguration)
        {
            return aConfiguration.AllowedStructures.Any(C =>
                C.EntityType == (EntityType)aStructure.Data.type &&
                C.FactionGroups == (FactionGroups)aStructure.Data.factionGroup
            );
        }

        private async Task ListAllTeleporterRoutes(int aPlayerId)
        {
            var Timer = new Stopwatch();
            Timer.Start();

            var G = await Request_GlobalStructure_List();
            Timer.Stop();

            var P = await Request_Player_Info(aPlayerId.ToId());

            await ShowDialog(aPlayerId, P, $"Teleporters (Playfields #{G.globalStructures.Count} Structures #{G.globalStructures.Aggregate(0, (c, p) => c + p.Value.Count)} load {Timer.Elapsed.TotalMilliseconds:N2}ms)", TeleporterDB.Settings.Current.TeleporterRoutes.OrderBy(T => T.Permission).Aggregate("\n", (S, T) => S + T.ToString(G) + "\n"));
        }

        private async Task DeleteTeleporterRoutes(int aPlayerId, int aSourceId, int aTargetId)
        {
            var P = await Request_Player_Info(aPlayerId.ToId());

            var deletedCount = TeleporterDB.Delete(aSourceId, aTargetId);
            TeleporterDB.Settings.Save();

            AlertPlayer(P.entityId, $"Delete {deletedCount} teleporter from {aSourceId}");
        }

        private async Task ListTeleporterRoutes(int aPlayerId, int aStructureId)
        {
            var G = await Request_GlobalStructure_List();
            var P = await Request_Player_Info(aPlayerId.ToId());

            await ShowDialog(aPlayerId, P, "Teleporters", TeleporterDB.List(aStructureId, P).OrderBy(T => T.Permission).Aggregate("\n", (S, T) => S + T.ToString(G) + "\n"));
        }

    private async Task<bool> ExecTeleportPlayer(GlobalStructureList aGlobalStructureList, PlayerInfo aPlayer, int aPlayerId)
        {
            var FoundRoute = TeleporterDB.SearchRoute(aGlobalStructureList, aPlayer);
            if (FoundRoute == null)
            {
                InformPlayer(aPlayerId, "No teleporter position here :-( wait 2min for structure update and try it again please.");
                log($"EmpyrionTeleporter: Exec: {aPlayer.playerName}/{aPlayer.entityId}/{aPlayer.clientId} -> no route found for pos={GetVector3(aPlayer.pos).String()} on '{aPlayer.playfield}'", LogLevel.Error);
                return false;
            }

            if (TeleporterDB.Settings.Current.ForbiddenPlayfields.Contains(aPlayer   .playfield) ||
                TeleporterDB.Settings.Current.ForbiddenPlayfields.Contains(FoundRoute.Playfield))
            {
                InformPlayer(aPlayerId, "No teleport allowed here ;-)");
                log($"EmpyrionTeleporter: Exec: {aPlayer.playerName}/{aPlayer.entityId}/{aPlayer.clientId} -> no teleport allowed for pos={GetVector3(aPlayer.pos).String()} on '{aPlayer.playfield}'", LogLevel.Error);
                return false;
            }

            log($"EmpyrionTeleporter: Exec: {aPlayer.playerName}/{aPlayer.entityId}-> {FoundRoute.Id} on '{FoundRoute.Playfield}' pos={FoundRoute.Position.String()} rot={FoundRoute.Rotation.String()}", LogLevel.Message);

            if (!PlayerLastGoodPosition.ContainsKey(aPlayer.entityId)) PlayerLastGoodPosition.Add(aPlayer.entityId, null);
            PlayerLastGoodPosition[aPlayer.entityId] = new IdPlayfieldPositionRotation(aPlayer.entityId, aPlayer.playfield, aPlayer.pos, aPlayer.rot);

            Action<PlayerInfo> ActionTeleportPlayer = async (P) =>
            {
                if (FoundRoute.Playfield == P.playfield)
                    try
                    {
                        await Request_Entity_Teleport(new IdPositionRotation(aPlayer.entityId, GetVector3(FoundRoute.Position), GetVector3(FoundRoute.Rotation)));
                    }
                    catch (Exception error)
                    {
                        InformPlayer(aPlayerId, $"Entity_Teleport: {error}");
                    }
                else
                {
                    try
                    {
                        await Request_Player_ChangePlayerfield(new IdPlayfieldPositionRotation(aPlayer.entityId, FoundRoute.Playfield, GetVector3(FoundRoute.Position), GetVector3(FoundRoute.Rotation)));
                    }
                    catch (Exception error)
                    {
                        InformPlayer(aPlayerId, $"Player_ChangePlayerfield: {error}");
                    }
                }
            };

            await Request_Player_SetPlayerInfo(new PlayerInfoSet() { entityId = aPlayer.entityId, health = (int)aPlayer.healthMax });

            new Thread(new ThreadStart(() =>
            {
                var TryTimer = new Stopwatch();
                TryTimer.Start();
                while (TryTimer.ElapsedMilliseconds < (TeleporterDB.Settings.Current.PreparePlayerForTeleport * 1000))
                {
                    Thread.Sleep(2000);
                    var WaitTime = TeleporterDB.Settings.Current.PreparePlayerForTeleport - (int)(TryTimer.ElapsedMilliseconds / 1000);
                    InformPlayer(aPlayerId, $"Prepare for teleport in {WaitTime} sec.");
                }

                ActionTeleportPlayer(aPlayer);
                CheckPlayerStableTargetPos(aPlayerId, aPlayer, ActionTeleportPlayer, FoundRoute.Position);

            })).Start();

            return true;
        }

        private void CheckPlayerStableTargetPos(int aPlayerId, PlayerInfo aCurrentPlayerInfo, Action<PlayerInfo> ActionTeleportPlayer, Vector3 aTargetPos)
        {
            new Thread(new ThreadStart(async () =>
            {
                PlayerInfo LastPlayerInfo = aCurrentPlayerInfo;

                await Request_Player_SetPlayerInfo(new PlayerInfoSet() { entityId = aCurrentPlayerInfo.entityId, health = (int)aCurrentPlayerInfo.healthMax });
                await Request_Player_AddItem(new IdItemStack(aCurrentPlayerInfo.entityId, new ItemStack(2389, 1))); // Bandages ;-)

                var TryTimer = new Stopwatch();
                TryTimer.Start();
                while (TryTimer.ElapsedMilliseconds < (TeleporterDB.Settings.Current.HoldPlayerOnPositionAfterTeleport * 1000))
                {
                    Thread.Sleep(2000);
                    var WaitTime = TeleporterDB.Settings.Current.HoldPlayerOnPositionAfterTeleport - (int)(TryTimer.ElapsedMilliseconds / 1000);

                    try
                    {
                        var P = await Request_Player_Info(aPlayerId.ToId());
                        LastPlayerInfo = P;
                        if (WaitTime > 0) InformPlayer(aPlayerId, $"Target reached please wait for {WaitTime} sec.");
                    }
                    catch (Exception error)
                    {
                        InformPlayer(aPlayerId, $"Target reached. {error}");
                    }
                }
                if (Vector3.Distance(GetVector3(LastPlayerInfo.pos), aTargetPos) > 3) ActionTeleportPlayer(LastPlayerInfo);
                await Request_Player_SetPlayerInfo(new PlayerInfoSet() { entityId = aCurrentPlayerInfo.entityId, health = (int)aCurrentPlayerInfo.healthMax });
                InformPlayer(aPlayerId, $"Thank you for traveling with the EmpyrionTeleporter :-)");
            })).Start();
        }

        private async Task ExecTeleportPlayerBack(int aPlayerId)
        {
            var P = await Request_Player_Info(aPlayerId.ToId());

            if (!PlayerLastGoodPosition.ContainsKey(P.entityId))
            {
                InformPlayer(aPlayerId, "No back teleport available.");
                return;
            }

            var LastGoodPos = PlayerLastGoodPosition[P.entityId];
            PlayerLastGoodPosition.Remove(P.entityId);

            if (LastGoodPos.playfield == P.playfield) await Request_Entity_Teleport(new IdPositionRotation(P.entityId, LastGoodPos.pos, LastGoodPos.rot));
            else                                      await Request_Player_ChangePlayerfield(LastGoodPos);
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
            log($"{aPrefix} Error: {aError.errorType} {aError.ToString()}", LogLevel.Error);
        }

        private int getIntParam(Dictionary<string, string> aArgs, string aParameterName)
        {
            string valueStr;
            if (!aArgs.TryGetValue(aParameterName, out valueStr)) return 0;

            int value;
            if (!int.TryParse(valueStr, out value)) return 0;

            return value;
        }

        private void DisplayHelp(int aPlayerId)
        {
            base.DisplayHelp(aPlayerId,
                    $"\n\nCosts teleporter set: [c][ff0000]{TeleporterDB.Settings.Current.CostsPerTeleporterPosition}[-][/c] credits" +
                    $"\nCosts teleporter use: [c][ff0000]{TeleporterDB.Settings.Current.CostsPerTeleport}[-][/c] credits" +
                    TeleporterDB.Settings.Current.AllowedStructures.Aggregate("\n\n[c][00ffff]Teleporter allowed at:[-][/c]", (s, a) => s + $"\n {a.EntityType}/{a.FactionGroups}")
            );
        }

    }
}
