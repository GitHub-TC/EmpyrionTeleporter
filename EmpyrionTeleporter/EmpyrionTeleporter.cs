﻿using Eleon.Modding;
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

            Log($"**HandleEmpyrionTeleporter loaded: {string.Join(" ", Environment.GetCommandLineArgs())}", LogLevel.Message);

            InitializeTeleporterDB();
            LogLevel = TeleporterDB.Settings.Current.LogLevel;
            ChatCommandManager.CommandPrefix = TeleporterDB.Settings.Current.ChatCommandPrefix;

            ChatCommands.Add(new ChatCommand(@"tt",                                            (I, A) => ExecAlignCommand(SubCommand.Teleport, TeleporterPermission.PublicAccess,       I, A), "Execute teleport"));
            ChatCommands.Add(new ChatCommand(@"tt help",                                       (I, A) => ExecAlignCommand(SubCommand.Help,     TeleporterPermission.PublicAccess,       I, A), "Display help"));
            ChatCommands.Add(new ChatCommand(@"tt back",                                       (I, A) => ExecAlignCommand(SubCommand.Back,     TeleporterPermission.PublicAccess,       I, A), "Teleports the player back to the last (good) position"));
            ChatCommands.Add(new ChatCommand(@"tt delete (?<SourceId>\d+) (?<TargetId>\d+)",   (I, A) => ExecAlignCommand(SubCommand.Delete,   TeleporterPermission.PublicAccess,       I, A), "Delete all teleportdata from {SourceId} {TargetId}"));
            ChatCommands.Add(new ChatCommand(@"tt list (?<Id>\d+)",                            (I, A) => ExecAlignCommand(SubCommand.List,     TeleporterPermission.PublicAccess,       I, A), "List all teleportdata from {Id}"));
            ChatCommands.Add(new ChatCommand(@"tt listall",                                    (I, A) => ExecAlignCommand(SubCommand.ListAll,  TeleporterPermission.PublicAccess,       I, A), "List all teleportdata", PermissionType.Moderator));
            ChatCommands.Add(new ChatCommand(@"tt cleanup",                                    (I, A) => ExecAlignCommand(SubCommand.CleanUp,  TeleporterPermission.PublicAccess,       I, A), "Removes all teleportdata to deleted structures", PermissionType.Moderator));
            ChatCommands.Add(new ChatCommand(@"tt private (?<SourceId>\d+) (?<TargetId>\d+)",  (I, A) => ExecAlignCommand(SubCommand.Save,     TeleporterPermission.PrivateAccess,      I, A), "Init Teleport from {SourceId} (PlayerPosition) to {TargetId} accessible is allowed for you only - must be initialized at {TargetId} too :-)"));
            ChatCommands.Add(new ChatCommand(@"tt faction (?<SourceId>\d+) (?<TargetId>\d+)",  (I, A) => ExecAlignCommand(SubCommand.Save,     TeleporterPermission.FactionAccess,      I, A), "Init Teleport from {SourceId} (PlayerPosition) to {TargetId} accessible is allowed for your faction - must be initialized at {TargetId} too :-)"));
            ChatCommands.Add(new ChatCommand(@"tt allies (?<SourceId>\d+) (?<TargetId>\d+)",   (I, A) => ExecAlignCommand(SubCommand.Save,     TeleporterPermission.AlliesAccess,       I, A), "Init Teleport from {SourceId} (PlayerPosition) to {TargetId} accessible is allowed for your faction and allies - must be initialized at {TargetId} too :-)"));
            ChatCommands.Add(new ChatCommand(@"tt free (?<SourceId>\d+) (?<TargetId>\d+)",     (I, A) => ExecAlignCommand(SubCommand.Save,     TeleporterPermission.PublicAccessFree,   I, A), "Init Teleport from {SourceId} (PlayerPosition) to {TargetId} accessible is allowed for everyone (use has no casts) - must be initialized at {TargetId} too :-)"));
            ChatCommands.Add(new ChatCommand(@"tt (?<SourceId>\d+) (?<TargetId>\d+)",          (I, A) => ExecAlignCommand(SubCommand.Save,     TeleporterPermission.PublicAccess,       I, A), "Init Teleport from {SourceId} (PlayerPosition) to {TargetId} accessible is allowed for everyone - must be initialized at {TargetId} too :-)"));
        }

        private void InitializeTeleporterDB()
        {
            TeleporterDB.LogDB     = Log;
            TeleporterDB.ModAccess = this;
            TeleporterDB = new TeleporterDB(Path.Combine(EmpyrionConfiguration.SaveGameModPath, "Teleporters.json"))
            {
                AreAllies = AreAllies
            };
        }

        private async Task<bool> AreAllies(int factionId, int testFactionId)
        {
            if (factionId == testFactionId) return true;

            var allFactions = (await Request_Get_Factions(new Id(0))).factions;
            var f1 = allFactions.FirstOrDefault(F => F.factionId == factionId);
            var f2 = allFactions.FirstOrDefault(F => F.factionId == testFactionId);

            var allied = f1.origin == f2.origin;  // default allied

            var allies = await Request_AlliancesAll();
            var allyTest1 = f1.factionId << 16 | f2.factionId;
            var allyTest2 = f2.factionId << 16 | f1.factionId;

            if (allies.alliances != null && (allies.alliances.Contains(allyTest1) || allies.alliances.Contains(allyTest2))) allied = !allied; // default changed

            Log($"AreAlliesResult:{allied}\n" +
                $"Routefaction:{factionId}/{GetName(allFactions, factionId)} Origin:{f1.origin} Playerfaction:{testFactionId}/{GetName(allFactions, testFactionId)} Origin:{f2.origin}" +
                allies.alliances?.Aggregate($"\nFactions:#{allFactions.Count} AlliesChange:#{allies.alliances.Count}", (S, A) => S + $"\nChangeallied:{A >> 16}/{GetName(allFactions, A >> 16)} <=> {A & 0x0000ffff}/{GetName(allFactions, A & 0x0000ffff)}"),
                LogLevel.Message);

            return allied;
        }

        private static string GetName(List<FactionInfo> allFactions, int A)
        {
            var findFaction = allFactions.FirstOrDefault(F => F.factionId == A);
            return findFaction.factionId == 0 ? "???" : $"{findFaction.abbrev}";
        }

        enum ChatType
        {
            Global = 3,
            Faction = 5,
        }

        private async Task ExecAlignCommand(SubCommand aCommand, TeleporterPermission aPermission, ChatInfo info, Dictionary<string, string> args)
        {
            Log($"**HandleEmpyrionTeleporter {info.type}:{info.msg} {args.Aggregate("", (s, i) => s + i.Key + "/" + i.Value + " ")}", LogLevel.Message);

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
            var TeleporterFlatIdList = TeleporterDB.Settings.Current.TeleporterRoutes.Aggregate(new List<int>(), (L, P) => { L.Add(P.A.Id); L.Add(P.B.Id); return L; });

            for (int i = TeleporterFlatIdList.Count - 1; i >= 0; i--)
            {
                if ((await Request_GlobalStructure_Info(TeleporterFlatIdList[i].ToId())).id == 0) TeleporterFlatIdList.Remove(i);
            }

            var DeleteList = TeleporterFlatIdList.Distinct();
            var DelCount = DeleteList.Aggregate(0, (C, I) => C + TeleporterDB.Delete(I, 0));
            Log($"CleanUpTeleporterRoutes: {DelCount} Structures: {DeleteList.Aggregate("", (S, I) => S + "," + I)}", LogLevel.Message);
            InformPlayer(aPlayerId, $"CleanUp: {DelCount} TeleporterRoutes");

            if (DelCount > 0) TeleporterDB.Settings.Save();
        }

        private async Task TeleportPlayer(int aPlayerId)
        {
            var P = await Request_Player_Info(aPlayerId.ToId());

            var FoundRoute = await TeleporterDB.SearchRoute(P);
            if (FoundRoute == null)
            {
                InformPlayer(aPlayerId, $"No teleporter position here {GetVector3(P.pos).String()} on '{P.playfield}' :-( wait 2min for structure update and try it again please.");
                Log($"EmpyrionTeleporter: Exec: {P.playerName}/{P.entityId}/{P.clientId} -> no route found for pos={GetVector3(P.pos).String()} on '{P.playfield}'", LogLevel.Error);
                return;
            }

            if(FoundRoute.Permission == TeleporterPermission.PublicAccessFree) { await ExecTeleportPlayer(P, aPlayerId, FoundRoute); return; }
            else if (P.credits < TeleporterDB.Settings.Current.CostsPerTeleport) AlertPlayer(P.entityId, $"You need {TeleporterDB.Settings.Current.CostsPerTeleport} credits ;-)");
            else if (await ExecTeleportPlayer(P, aPlayerId, FoundRoute) && TeleporterDB.Settings.Current.CostsPerTeleport > 0) await Request_Player_SetCredits(new IdCredits(P.entityId, P.credits - TeleporterDB.Settings.Current.CostsPerTeleport));
        }

        private async Task SaveTeleporterRoute(int aPlayerId, TeleporterPermission aPermission, int aSourceId, int aTargetId)
        {
            var P = await Request_Player_Info(aPlayerId.ToId());

            var SourceStructure = await TeleporterDB.SearchEntity(aSourceId);
            var TargetStructure = await TeleporterDB.SearchEntity(aTargetId);

            if (SourceStructure == null) AlertPlayer(P.entityId, $"Structure not found: {aSourceId}");
            else if (TargetStructure == null) AlertPlayer(P.entityId, $"Structure not found: {aTargetId}");
            else if (!CheckPermission(SourceStructure, TeleporterDB.Settings.Current)) AlertPlayer(P.entityId, $"Structure not allowed: {SourceStructure.Data.name}({aSourceId}) {(EntityType)SourceStructure.Data.type}/{(FactionGroups)SourceStructure.Data.factionGroup}");
            else if (!CheckPermission(TargetStructure, TeleporterDB.Settings.Current)) AlertPlayer(P.entityId, $"Structure not allowed: {TargetStructure.Data.name}({aTargetId}) {(EntityType)TargetStructure.Data.type}/{(FactionGroups)TargetStructure.Data.factionGroup}");
            else if (TeleporterDB.Settings.Current.ForbiddenPlayfields.Contains(P.playfield)               ||
                        TeleporterDB.Settings.Current.ForbiddenPlayfields.Contains(SourceStructure.Playfield) ||
                        TeleporterDB.Settings.Current.ForbiddenPlayfields.Contains(TargetStructure.Playfield))
            {
                InformPlayer(aPlayerId, "No teleporter allowed here ;-)");
                Log($"EmpyrionTeleporter: Exec: {P.playerName}/{P.entityId}/{P.clientId} -> no teleport allowed for pos={GetVector3(P.pos).String()} on '{P.playfield}'", LogLevel.Error);
            }
            else
            {
                var foundRoute  = TeleporterDB.SearchRoute(aPermission, aSourceId, aTargetId, P);
                var routeUpdate = foundRoute != null && foundRoute.A.Position != Vector3.Zero && foundRoute.B.Position != Vector3.Zero;

                if (!routeUpdate && P.credits < TeleporterDB.Settings.Current.CostsPerTeleporterPosition)
                {
                    AlertPlayer(P.entityId, $"You need {TeleporterDB.Settings.Current.CostsPerTeleporterPosition} credits ;-)");
                    return;
                }

                await TeleporterDB.AddRoute(aPermission, aSourceId, aTargetId, P);
                TeleporterDB.Settings.Save();

                if (!routeUpdate && TeleporterDB.Settings.Current.CostsPerTeleporterPosition > 0) await Request_Player_SetCredits(new IdCredits(P.entityId, P.credits - TeleporterDB.Settings.Current.CostsPerTeleporterPosition));

                await ShowDialog(aPlayerId, P, "Teleporters", $"\nTeleporter set\n{SourceStructure.Data.name}({aSourceId}) => {TargetStructure.Data.name}({aTargetId})");
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
            var P = await Request_Player_Info(aPlayerId.ToId());

            await ShowDialog(aPlayerId, P, $"Teleporters", TeleporterDB.Settings.Current.TeleporterRoutes.OrderBy(T => T.Permission).Aggregate("\n", (S, T) => S + T.ToInfoString() + "\n"));
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
            var P = await Request_Player_Info(aPlayerId.ToId());

            await ShowDialog(aPlayerId, P, "Teleporters", TeleporterDB.List(aStructureId, P).OrderBy(T => T.Permission).Aggregate("\n", (S, T) => S + T.ToInfoString() + "\n"));
        }

        private async Task<bool> ExecTeleportPlayer(PlayerInfo aPlayer, int aPlayerId, TeleporterDB.TeleporterTargetData foundRoute)
        {
            if (TeleporterDB.Settings.Current.ForbiddenPlayfields.Contains(aPlayer   .playfield) ||
                TeleporterDB.Settings.Current.ForbiddenPlayfields.Contains(foundRoute.Playfield))
            {
                InformPlayer(aPlayerId, "No teleport allowed here ;-)");
                Log($"EmpyrionTeleporter: Exec: {aPlayer.playerName}/{aPlayer.entityId}/{aPlayer.clientId} -> no teleport allowed for pos={GetVector3(aPlayer.pos).String()} on '{aPlayer.playfield}'", LogLevel.Error);
                return false;
            }

            Log($"EmpyrionTeleporter: Exec: {aPlayer.playerName}/{aPlayer.entityId}-> {foundRoute.Id} on '{foundRoute.Playfield}' pos={foundRoute.Position.String()} rot={foundRoute.Rotation.String()}", LogLevel.Message);

            if (!PlayerLastGoodPosition.ContainsKey(aPlayer.entityId)) PlayerLastGoodPosition.Add(aPlayer.entityId, null);
            PlayerLastGoodPosition[aPlayer.entityId] = new IdPlayfieldPositionRotation(aPlayer.entityId, aPlayer.playfield, aPlayer.pos, aPlayer.rot);

            Action<PlayerInfo> ActionTeleportPlayer = async (P) =>
            {
                if (foundRoute.Playfield == P.playfield)
                    try
                    {
                        await Request_Entity_Teleport(new IdPositionRotation(aPlayer.entityId, GetVector3(foundRoute.Position), GetVector3(foundRoute.Rotation)));
                    }
                    catch (Exception error)
                    {
                        Log($"Entity_Teleport [{aPlayerId} {P.playerName}]: {error}", LogLevel.Error);
                        InformPlayer(aPlayerId, $"Entity_Teleport: {error.Message}");
                    }
                else
                {
                    try
                    {
                        await Request_Player_ChangePlayerfield(new IdPlayfieldPositionRotation(aPlayer.entityId, foundRoute.Playfield, GetVector3(foundRoute.Position), GetVector3(foundRoute.Rotation)));
                    }
                    catch (Exception error)
                    {
                        Log($"Player_ChangePlayerfield [{aPlayerId} {P.playerName}]: {error}", LogLevel.Error);
                        InformPlayer(aPlayerId, $"Player_ChangePlayerfield: {error.Message}");
                    }
                }
            };

            try
            {
                await Request_Player_SetPlayerInfo(new PlayerInfoSet() { entityId = aPlayer.entityId, health = (int)aPlayer.healthMax });
            }
            catch (Exception error)
            {
                Log($"Player_SetHealth [{aPlayerId} {aPlayer.playerName}]: {error}", LogLevel.Error);
                InformPlayer(aPlayerId, $"Player_SetHealth: {error.Message}");
            }

            new Thread(new ThreadStart(() =>
            {
                var TryTimer = new Stopwatch();
                TryTimer.Start();
                while (TryTimer.ElapsedMilliseconds < (TeleporterDB.Settings.Current.PreparePlayerForTeleport * 1000))
                {
                    var WaitTime = TeleporterDB.Settings.Current.PreparePlayerForTeleport - (int)(TryTimer.ElapsedMilliseconds / 1000);
                    InformPlayer(aPlayerId, WaitTime > 1 ? $"Prepare for teleport in {WaitTime} sec." : $"Prepare for teleport now.");
                    if(WaitTime > 0) Thread.Sleep(2000);
                }

                ActionTeleportPlayer(aPlayer);
                CheckPlayerStableTargetPos(aPlayerId, aPlayer, ActionTeleportPlayer, foundRoute.Position);

            })).Start();

            return true;
        }

        private void CheckPlayerStableTargetPos(int aPlayerId, PlayerInfo aCurrentPlayerInfo, Action<PlayerInfo> ActionTeleportPlayer, Vector3 aTargetPos)
        {
            new Thread(new ThreadStart(async () =>
            {
                try
                {
                    PlayerInfo LastPlayerInfo = aCurrentPlayerInfo;

                    await Request_Player_SetPlayerInfo(new PlayerInfoSet() { entityId = aCurrentPlayerInfo.entityId, health = (int)aCurrentPlayerInfo.healthMax });
                    if (TeleporterDB.Settings.Current.HealthPack > 0) await Request_Player_AddItem(new IdItemStack(aCurrentPlayerInfo.entityId, new ItemStack(TeleporterDB.Settings.Current.HealthPack, 1))); // Bandages ;-)

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
                }
                catch (Exception error)
                {
                    Log($"CheckPlayerStableTargetPos: {error}", LogLevel.Error);
                }
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
            Log($"{aPrefix} Error: {aError.errorType} {aError.ToString()}", LogLevel.Error);
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
