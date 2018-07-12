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

namespace EmpyrionTeleporter
{
    public static class Vector3Extension{
        public static string String(this PVector3 aVector) => $"{aVector.x},{aVector.y},{aVector.z}";
        public static string String(this Vector3 aVector) => $"{aVector.X},{aVector.Y},{aVector.Z}";
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

        enum SubCommand
        {
            Help,
            Back,
            Delete,
            List,
            ListAll,
            Save,
            Teleport
        }

        public override void Initialize(ModGameAPI aGameAPI)
        {
            GameAPI = aGameAPI;
            verbose = true;

            log($"**HandleEmpyrionTeleporter loaded");

            TeleporterDB.LogDB = log;
            TeleporterDB = TeleporterDB.ReadDB();

            ChatCommands.Add(new ChatCommand(@"/tt",                                            (I, A) => ExecAlignCommand(SubCommand.Teleport, TeleporterPermission.PublicAccess,  I, A), "Execute teleport"));
            ChatCommands.Add(new ChatCommand(@"/tt help",                                       (I, A) => ExecAlignCommand(SubCommand.Help,     TeleporterPermission.PublicAccess,  I, A), "Display help"));
            ChatCommands.Add(new ChatCommand(@"/tt back",                                       (I, A) => ExecAlignCommand(SubCommand.Back,     TeleporterPermission.PublicAccess,  I, A), "Teleports the player back to the last (good) position"));
            ChatCommands.Add(new ChatCommand(@"/tt delete (?<Id>\d+)",                          (I, A) => ExecAlignCommand(SubCommand.Delete,   TeleporterPermission.PublicAccess,  I, A), "Delete all teleportdata from {Id}"));
            ChatCommands.Add(new ChatCommand(@"/tt list (?<Id>\d+)",                            (I, A) => ExecAlignCommand(SubCommand.List,     TeleporterPermission.PublicAccess,  I, A), "List all teleportdata from {Id}"));
            ChatCommands.Add(new ChatCommand(@"/tt listall",                                    (I, A) => ExecAlignCommand(SubCommand.ListAll,  TeleporterPermission.PublicAccess,  I, A), "List all teleportdata", PermissionType.Moderator));
            ChatCommands.Add(new ChatCommand(@"/tt private (?<SourceId>\d+) (?<TargetId>\d+)",  (I, A) => ExecAlignCommand(SubCommand.Save,     TeleporterPermission.PrivateAccess, I, A), "Init Teleport from {SourceId} (PlayerPosition) to {TargetId} accessible is allowed for you only - must be initialized at {TargetId} too :-)"));
            ChatCommands.Add(new ChatCommand(@"/tt faction (?<SourceId>\d+) (?<TargetId>\d+)",  (I, A) => ExecAlignCommand(SubCommand.Save,     TeleporterPermission.FactionAccess, I, A), "Init Teleport from {SourceId} (PlayerPosition) to {TargetId} accessible is allowed for your faction - must be initialized at {TargetId} too :-)"));
            ChatCommands.Add(new ChatCommand(@"/tt (?<SourceId>\d+) (?<TargetId>\d+)",          (I, A) => ExecAlignCommand(SubCommand.Save,     TeleporterPermission.PublicAccess,  I, A), "Init Teleport from {SourceId} (PlayerPosition) to {TargetId} accessible is allowed for everyone - must be initialized at {TargetId} too :-)"));
        }

        enum ChatType
        {
            Global  = 3,
            Faction = 5,
        }

        private void ExecAlignCommand(SubCommand aCommand, TeleporterPermission aPermission, ChatInfo info, Dictionary<string, string> args)
        {
            log($"**HandleEmpyrionTeleporter {info.type}:{info.msg} {args.Aggregate("", (s, i) => s + i.Key + "/" + i.Value + " ")}");

            if (info.type != (byte)ChatType.Faction) return;

            switch (aCommand)
            {
                case SubCommand.Help    : DisplayHelp               (info.playerId); break;
                case SubCommand.Back    : ExecTeleportPlayerBack    (info.playerId); break;
                case SubCommand.Delete  : DeleteTeleporterRoutes    (info.playerId, getIntParam(args, "Id")); break;
                case SubCommand.List    : ListTeleporterRoutes      (info.playerId, getIntParam(args, "Id")); break;
                case SubCommand.ListAll : ListAllTeleporterRoutes   (info.playerId); break;
                case SubCommand.Save    : SaveTeleporterRoute       (info.playerId, aPermission, getIntParam(args, "SourceId"), getIntParam(args, "TargetId")); break;
                case SubCommand.Teleport: Request_GlobalStructure_List(G => Request_Player_Info(info.playerId.ToId(), P => ExecTeleportPlayer(G, P))); return;
            }
        }


        /*
                private void UpdateTeleporterPos(GlobalStructureList aGlobalStructureList, Action aAction)
                {
                    var UpdateIds = TeleporterDB.TeleporterRoutes.Aggregate(new List<int>(), (L, T) => { if (T.A.Id != 0 && !L.Contains(T.A.Id)) L.Add(T.A.Id); if (T.B.Id != 0 && !L.Contains(T.B.Id)) L.Add(T.B.Id); return L; });
                    UpdateTeleporterPos(UpdateIds, aGlobalStructureList, aAction);
                }

                private void UpdateTeleporterPos(List<int> aUpdateIds, GlobalStructureList aGlobalStructureList, Action aAction)
                {
                    File.AppendAllText("info.txt", $"\n{DateTime.Now.ToLongTimeString()} Update.Count={aUpdateIds.Count}");

                    if (aUpdateIds.Count == 0)
                    {
                        aAction();
                        return;
                    }

                    var UpdateId = aUpdateIds.First();
                    aUpdateIds.RemoveAt(0);

                    try
                    {
                        File.AppendAllText("info.txt", $"\n{DateTime.Now.ToLongTimeString()} Update={UpdateId}");
                        Request_Entity_PosAndRot(new Id(UpdateId), P => { UpdateTeleporterPos(aGlobalStructureList, P); }, E => UpdateTeleporterPos(aUpdateIds, aGlobalStructureList, aAction));
                    }
                    catch (Exception)
                    {
                        UpdateTeleporterPos(aUpdateIds, aGlobalStructureList, aAction);
                    }
                }

                private void UpdateTeleporterPos(GlobalStructureList aGlobalStructureList, IdPositionRotation aIdPos)
                {
                    bool Leave = false;
                    foreach (var PlayfieldItems in aGlobalStructureList.globalStructures.ToArray())
                    {
                        aGlobalStructureList.globalStructures[PlayfieldItems.Key] = PlayfieldItems.Value.Select(E => {
                            if (E.id == aIdPos.id) {
                                File.AppendAllText("info.txt", $"\n{DateTime.Now.ToLongTimeString()} pos={E.pos.String()}=>{aIdPos.pos.String()} rot={E.rot.String()}=>{aIdPos.rot.String()}");
                                E.pos = aIdPos.pos; E.rot = aIdPos.rot; Leave = true;
                            }
                            return E;
                        }).ToList();

                        if (Leave) return;
                    }
                }
        */

        private void SaveTeleporterRoute(int aPlayerId, TeleporterPermission aPermission, int aSourceId, int aTargetId)
        {
            Request_GlobalStructure_List(G =>
            {
                Request_Player_Info(aPlayerId.ToId(), (P) =>
                {
                    if      (TeleporterDB.SearchEntity(G, aSourceId) == null) AlertPlayer(P.entityId, $"Structure not found: {aSourceId}");
                    else if (TeleporterDB.SearchEntity(G, aTargetId) == null) AlertPlayer(P.entityId, $"Structure not found: {aTargetId}");
                    else
                    {
                        TeleporterDB.AddRoute(G, aPermission, aSourceId, aTargetId, P);
                        TeleporterDB.SaveDB();
                    }
                });
            });
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
                var deletedCount = TeleporterDB.Delete(aStructureId, P);
                TeleporterDB.SaveDB();

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

        private void ExecTeleportPlayer(GlobalStructureList aGlobalStructureList, PlayerInfo aPlayer)
        {
            var FoundRoute = TeleporterDB.SearchRoute(aGlobalStructureList, aPlayer);
            if (FoundRoute == null)
            {
                InformPlayer(aPlayer.entityId, "No teleporter position here :-( wait 2min for structure update and try it again please.");
                log($"EmpyrionTeleporter: Exec: {aPlayer.playerName}/{aPlayer.entityId}-> no route found for pos={GetVector3(aPlayer.pos).String()} on '{aPlayer.playfield}'");
                return;
            }

            log($"EmpyrionTeleporter: Exec: {aPlayer.playerName}/{aPlayer.entityId}-> {FoundRoute.Id} on '{FoundRoute.Playfield}' pos={FoundRoute.Position.String()} rot={FoundRoute.Rotation.String()}");

            if (!PlayerLastGoodPosition.ContainsKey(aPlayer.entityId)) PlayerLastGoodPosition.Add(aPlayer.entityId, null);
            PlayerLastGoodPosition[aPlayer.entityId] = new IdPlayfieldPositionRotation(aPlayer.entityId, aPlayer.playfield, aPlayer.pos, aPlayer.rot);

            if (FoundRoute.Playfield == aPlayer.playfield) Request_Entity_Teleport         (new IdPositionRotation         (aPlayer.entityId,                       GetVector3(FoundRoute.Position), GetVector3(FoundRoute.Rotation)));
            else                                           Request_Player_ChangePlayerfield(new IdPlayfieldPositionRotation(aPlayer.entityId, FoundRoute.Playfield, GetVector3(FoundRoute.Position), GetVector3(FoundRoute.Rotation)));
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

                if (LastGoodPos.playfield == P.playfield) Request_Entity_Teleport         (new IdPositionRotation(P.entityId, LastGoodPos.pos, LastGoodPos.rot));
                else                                      Request_Player_ChangePlayerfield(LastGoodPos);
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
                MsgText = $"{aTitle}: {aPlayer.playerName} with permission {(PermissionType)aPlayer.permission}\n" + aMessage,
            });
        }

        private void DisplayHelp(int aPlayerId)
        {
            Request_Player_Info(aPlayerId.ToId(), (P) =>
            {
                ShowDialog(aPlayerId, P, "Commands", String.Join("\n", GetChatCommandsForPermissionLevel((PermissionType)P.permission).Select(x => x.ToString()).ToArray()));
            });
        }

    }
}
