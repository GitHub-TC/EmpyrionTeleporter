using System;
using Eleon.Modding;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Xml;
using System.Linq;
using System.Numerics;
using EmpyrionAPIDefinitions;

namespace EmpyrionTeleporter
{
    public enum TeleporterPermission
    {
        PrivateAccess,
        FactionAccess,
        PublicAccess
    }

    public class TeleporterDB
    {
        public class TeleporterData
        {
            public int Id { get; set; }
            public Vector3 Position { get; set; }
            public Vector3 Rotation { get; set; }
            public override string ToString()
            {
                return $"Id:[c][ffffff]{Id}[-][/c] relpos=[c][ffffff]{Position.String()}[-][/c]";
            }
        }

        public class TeleporterTargetData : TeleporterData
        {
            public string Playfield { get; set; }
            public override string ToString()
            {
                return $"Id:[c][ffffff]{Id}/[c][ffffff]{Playfield}[-][/c] relpos=[c][ffffff]{Position.String()}[-][/c]";
            }
        }

        public class TeleporterRoute
        {
            public TeleporterPermission Permission { get; set; }
            public int PermissionId { get; set; }
            public TeleporterData A { get; set; }
            public TeleporterData B { get; set; }

            public override string ToString()
            {
                return $"{Permission}{(PermissionId == 0 ? "" : $"[PermissionId]")}: {A.ToString()} <=> {B.ToString()}";
            }

            public string ToString(GlobalStructureList G)
            {
                var Sa = SearchEntity(G, A.Id);
                var Sb = SearchEntity(G, B.Id);

                return $"[c][ff0000]{Permission}{(PermissionId == 0 ? "" : $"[PermissionId]")}[-][/c]: " +
                       (Sa == null ? A.ToString() : $"[c][ff00ff]{Sa.Data.name}[-][/c] [[c][ffffff]{Sa.Data.id}[-][/c]/[c][ffffff]{Sa.Playfield}[-][/c]]") + " <=> " +
                       (Sb == null ? B.ToString() : $"[c][ff00ff]{Sb.Data.name}[-][/c] [[c][ffffff]{Sb.Data.id}[-][/c]/[c][ffffff]{Sb.Playfield}[-][/c]]");
            }
        }

        public Configuration Configuration { get; set; } = new Configuration();
        public List<TeleporterRoute> TeleporterRoutes { get; set; } = new List<TeleporterRoute>();
        public static Action<string, LogLevel> LogDB { get; set; }

        private static void log(string aText, LogLevel aLevel)
        {
            LogDB?.Invoke(aText, aLevel);
        }

        bool IsPermissionGranted(TeleporterRoute aRoute, PlayerInfo aPlayer)
        {
            return aRoute.Permission == TeleporterPermission.PublicAccess  ? true :
                   aRoute.Permission == TeleporterPermission.FactionAccess ? aRoute.PermissionId == aPlayer.factionId :
                   aRoute.Permission == TeleporterPermission.PrivateAccess ? aRoute.PermissionId == aPlayer.entityId  : false;
        }

        public void AddRoute(GlobalStructureList aGlobalStructureList, TeleporterPermission aPermission, int aSourceId, int aTargetId, PlayerInfo aPlayer)
        {
            var FoundEntity = SearchEntity(aGlobalStructureList, aSourceId);
            if (FoundEntity == null) return;

            var FoundRoute = TeleporterRoutes.FirstOrDefault(R => R.Permission == aPermission && IsPermissionGranted(R, aPlayer) &&
                    ((R.A.Id == aSourceId && R.B.Id == aTargetId) || (R.B.Id == aSourceId && R.A.Id == aTargetId)));

            var RelativePos = GetVector3(aPlayer.pos) - GetVector3(FoundEntity.Data.pos);
            var NormRot     = GetVector3(aPlayer.rot) - GetVector3(FoundEntity.Data.rot);

            var EntityRot = GetMatrix4x4(GetVector3(FoundEntity.Data.rot)).Transpose();

            RelativePos = Vector3.Transform(RelativePos, EntityRot);
            RelativePos = new Vector3(RelativePos.X, ((float)Math.Round(RelativePos.Y + 1.9) - 1), RelativePos.Z);

            if (FoundRoute == null)
            {
                TeleporterRoutes.Add(FoundRoute = new TeleporterRoute()
                {
                    Permission   = aPermission,
                    PermissionId = aPermission == TeleporterPermission.PublicAccess ? 0 :
                                   aPermission == TeleporterPermission.FactionAccess ? aPlayer.factionId :
                                   aPermission == TeleporterPermission.PrivateAccess ? aPlayer.entityId : 0,
                    A = new TeleporterData() { Id = aSourceId, Position = RelativePos, Rotation = NormRot },
                    B = new TeleporterData() { Id = aTargetId }
                });

                TeleporterRoutes = TeleporterRoutes.OrderBy(T => T.Permission).ToList();
            }
            else if (FoundRoute.A.Id == aSourceId && FoundRoute.B.Id == aTargetId)
            {
                FoundRoute.Permission = aPermission;
                FoundRoute.A.Position = RelativePos;
                FoundRoute.A.Rotation = NormRot;
            }
            else if (FoundRoute.A.Id == aTargetId && FoundRoute.B.Id == aSourceId)
            {
                FoundRoute.Permission = aPermission;
                FoundRoute.B.Position = RelativePos;
                FoundRoute.B.Rotation = NormRot;
            }
        }

        public class PlayfieldStructureInfo
        {
            public string Playfield { get; set; }
            public GlobalStructureInfo Data { get; set; }
        }

        public static PlayfieldStructureInfo SearchEntity(GlobalStructureList aGlobalStructureList, int aSourceId)
        {
            foreach (var TestPlayfieldEntites in aGlobalStructureList.globalStructures)
            {
                var FoundEntity = TestPlayfieldEntites.Value.FirstOrDefault(E => E.id == aSourceId);
                if (FoundEntity.id != 0) return new PlayfieldStructureInfo() { Playfield = TestPlayfieldEntites.Key, Data = FoundEntity };
            }
            return null;
        }

        public int Delete(int aSourceId, int aTargetId)
        {
            var OldCount = TeleporterRoutes.Count();
            TeleporterRoutes = aTargetId == 0
                ? TeleporterRoutes.Where(T => T.A.Id != aSourceId && T.B.Id != aSourceId)
                                  .ToList()
                : TeleporterRoutes.Where(T => !((T.A.Id == aSourceId && T.B.Id == aTargetId) ||
                                                (T.A.Id == aTargetId && T.B.Id == aSourceId)))
                                  .ToList();

            return OldCount - TeleporterRoutes.Count();
        }

        bool IsNearPos(GlobalStructureList aGlobalStructureList, TeleporterData aTarget, PVector3 aTestPos)
        {
            var StructureInfo = SearchEntity(aGlobalStructureList, aTarget.Id);
            if (StructureInfo == null)
            {
                log($"TargetStructure missing:{aTarget.Id} pos={aTarget.Position.String()}", LogLevel.Error);
                return false;
            }

            var StructureRotation = GetMatrix4x4(GetVector3(StructureInfo.Data.rot));

            var TeleporterPos = Vector3.Transform(aTarget.Position, StructureRotation) + GetVector3(StructureInfo.Data.pos);

            var Distance = Math.Abs(Vector3.Distance(TeleporterPos, GetVector3(aTestPos)));

            log($"FoundTarget:{StructureInfo.Data.id}/{StructureInfo.Data.type} pos={StructureInfo.Data.pos.String()} TeleportPos={TeleporterPos.String()} TEST {aTestPos.String()} => {Distance}", LogLevel.Message);

            return Distance < 4;
        }

        public IEnumerable<TeleporterRoute> List(int aStructureId, PlayerInfo aPlayer)
        {
            return TeleporterRoutes.Where(T => (T.A.Id == aStructureId || T.B.Id == aStructureId) && IsPermissionGranted(T, aPlayer));
        }

        TeleporterTargetData GetCurrentTeleportTargetPosition(GlobalStructureList aGlobalStructureList, TeleporterData aTarget)
        {
            var StructureInfo = SearchEntity(aGlobalStructureList, aTarget.Id);
            if (StructureInfo == null)
            {
                log($"TargetStructure missing:{aTarget.Id} pos={aTarget.Position.String()}", LogLevel.Error);
                return null;
            }

            var StructureInfoRot = GetVector3(StructureInfo.Data.rot);
            var StructureRotation = GetMatrix4x4(StructureInfoRot);
            var TeleportTargetPos = Vector3.Transform(aTarget.Position, StructureRotation) + GetVector3(StructureInfo.Data.pos);

            log($"CurrentTeleportTargetPosition:{StructureInfo.Data.id}/{StructureInfo.Data.type} pos={StructureInfo.Data.pos.String()} TeleportPos={TeleportTargetPos.String()}", LogLevel.Message);

            return new TeleporterTargetData() { Id = aTarget.Id, Playfield = StructureInfo.Playfield, Position = TeleportTargetPos, Rotation = aTarget.Rotation + StructureInfoRot};
        }

        bool IsZero(PVector3 aVector)
        {
            return aVector.x == 0 && aVector.y == 0 && aVector.z == 0;
        }

        public TeleporterTargetData SearchRoute(GlobalStructureList aGlobalStructureList, PlayerInfo aPlayer)
        {
            //log($"T:{TeleporterRoutes.Aggregate("", (s, t) => s + " " + t.ToString())} => {aGlobalStructureList.globalStructures.Aggregate("", (s, p) => s + p.Key + ":" + p.Value.Aggregate("", (ss, pp) => ss + " " + pp.id + "/" + pp.name))}");

            foreach (var I in TeleporterRoutes.Where(T => T.B.Position != Vector3.Zero && IsPermissionGranted(T, aPlayer)))
            {
                if (IsNearPos(aGlobalStructureList, I.A, aPlayer.pos)) return GetCurrentTeleportTargetPosition(aGlobalStructureList, I.B);
                if (IsNearPos(aGlobalStructureList, I.B, aPlayer.pos)) return GetCurrentTeleportTargetPosition(aGlobalStructureList, I.A);
            }
            return null;
        }

        public void SaveDB(string DBFileName)
        {
            var serializer = new XmlSerializer(typeof(TeleporterDB));
            Directory.CreateDirectory(Path.GetDirectoryName(DBFileName));
            using (var writer = XmlWriter.Create(DBFileName, new XmlWriterSettings() { Indent = true, IndentChars = "  " }))
            {
                serializer.Serialize(writer, this);
            }
        }

        public static TeleporterDB ReadDB(string DBFileName)
        {
            if (!File.Exists(DBFileName))
            {
                log($"TeleporterDB ReadDB not found '{DBFileName}'", LogLevel.Error);
                return new TeleporterDB();
            }

            try
            {
                log($"TeleporterDB ReadDB load '{DBFileName}'", LogLevel.Message);
                var serializer = new XmlSerializer(typeof(TeleporterDB));
                using (var reader = XmlReader.Create(DBFileName))
                {
                    return (TeleporterDB)serializer.Deserialize(reader);
                }
            }
            catch(Exception Error)
            {
                log("TeleporterDB ReadDB" + Error.ToString(), LogLevel.Error);
                return new TeleporterDB();
            }
        }

        public static Vector3 GetVector3(PVector3 aVector)
        {
            return new Vector3(aVector.x, aVector.y, aVector.z);
        }

        public static PVector3 GetVector3(Vector3 aVector)
        {
            return new PVector3(aVector.X, aVector.Y, aVector.Z);
        }

        public static Matrix4x4 GetMatrix4x4(Vector3 aVector)
        {
            return Matrix4x4.CreateFromYawPitchRoll(aVector.Y * (float)(Math.PI / 180), aVector.Z * (float)(Math.PI / 180), aVector.X * (float)(Math.PI / 180));
        }

    }
}
