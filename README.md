# Empyrion Teleporter
## FAQ

Eine übersetzte Version findet ihr im EmpyrionTeleporter/bin Verzeichnis falls ihr die Mod nicht selber prüfen und compilieren wollt ;-)

Oder hier:  https://empyriononline.com/threads/mod-empyrionteleporter.39200/

### Wo für ist das?

Ein Teleport zwischen zwei vorher eingerichteten Positionen. Dabei muss der Spieler zuvor an BEIDEN Orten gewesen sein und dort die Teleporterposition relativ zu einer Struktur festgelegt haben.

#### Wie steuert man den MOD?

Die Kommandos funktionieren NUR im Fraktionschat!

#### Hilfe

* /tt help : Zeigt die Kommandos der Mod an

#### Teleport

* /tt => Teleport von dieser (vorher eingerichteten) Position zum Ziel durchführen
* /tt help => Liste der Kommandos
* /tt back => Falls ein Teleport schiefgegenen sein sollte kann sich der Spieler hiermit zu der Position VOR dem Teleport zurück teleportieren lassen
* /tt delete <Id> => Löscht alle Teleporterouten von und nach <Id>
* /tt list <Id> => Listet alle Teleporterouten von und nach <Id> auf
* /tt listall => Listet alle Teleporterouten auf (nur ab Moderator erlaubt)
* /tt cleanup => Löscht alle Teleporterrouten die zu gelöschten Strukturen führen (nur ab Moderator erlaubt)
* /tt private <SourceId> <TargetId> => Privaten Teleportort von der aktuellen Spielerposition relativ zur <SourceId> nach <TargetId> einrichten der nur für den Spieler nutzbar ist.
* /tt faction <SourceId> <TargetId> => Fraktions Teleportort von der aktuellen Spielerposition relativ zur <SourceId> nach <TargetId> einrichten der nur für die Fraktion nutzbar ist.
* /tt <SourceId> <TargetId> => Öffentlichen Teleportort von der aktuellen Spielerposition relativ zur <SourceId> nach <TargetId> einrichten.

Beispiel:
- Basis: Akua (Id:1001)
- CV: Akua Orbit (Id:4004)

1. Auf/Bei der Basis die Position des Spielers markieren (Textur, LCD, Farbe,...) und das Kommando "/tt 1001 4004" aufrufen
2. Zum CV reisen (noch manuell) ;-)
3. Auf/Bei/In dem CV die Position des Spielers markieren (Textur, LCD, Farbe,...) und das Kommando "/tt 4004 1001" aufrufen

=> Nun ist eine Teleporterroute eingerichtet und kann von den beiden markierten Positionen aus benutzt werden in dem der Spieler, an der Position stehend, im Fraktionschat den Befehl /tt absetzt.

HINWEIS: Da Empyrion es mit den Positionen beim Teleport nicht so genau nimmt sollte der Raum um einen herum ausreichend Platz bieten ;-)

### Konfiguration
Eine Konfiguration kann man in der Datei (wird beim ersten Start automatisch erstellt)

[Empyrion Directory]\Saves\Games\[SaveGameName]\Mods\EmpyrionTeleporter\TeleporterDB.xml

vornehmen.

* HoldPlayerOnPositionAfterTeleport: Zeit in Sekunden die ein Spieler nach dem Teleport auf Position gehalten wird bis die Strukturen (hoffentlich) nachgeladen sind
* CostsPerTeleporterPosition: Creditkosten für das Setzen eines Teleporterpunktes
* CostsPerTeleport: Creditkosten für das Benutzen eines Teleporterpunktes
* AllowedStructures: Liste der erlaubten Strukturen für Teleporterpunkte hierbei sind folgende Werte erlaubt
  - EntityType: BA, CV, SV, HV 
  - FactionGroups: Faction, Player, Alien, Admin

### Was kommt noch?
Zunächst erstmal und damit viel Spaß beim Teleportieren wünscht euch

ASTIC/TC

***

English-Version:

---

## FAQ

You can find a compiled DLL version in the EmpyrionTeleporter/bin directory if you do not want to check and compile the mod myself ;-)

Or here: https://empyriononline.com/threads/mod-empyrionteleporter.39200/

### What is this?

A MOD which can be used to teleport players to previously configured positions. It's required that the player must visited the places before and the teleport positions got binded to the structure.

#### What are all the commands?

All commands only work in faction chat!

#### Help

* /tt help : show useful information and commands about the MOD

#### Teleport

* /tt : teleport the player from previously configured position to the destination
* /tt help : show all commands
* /tt back : if a teleport failed, the player can get back to the position he came from
* /tt delete <Id> : removes the teleport connection regarding this ID
* /tt list <Id> : show all teleport connections from and to this ID
* /tt listall : show all teleport connections (only Moderators can use it)
* /tt cleanup : remove all teleport connections to deleted structures (only Moderators can use it)
* /tt private <SourceId> <TargetId> : set a private teleport location from the player current position relative to the source ID and target ID. Private means only that player can use this teleporter
* /tt faction <SourceId> <TargetId> : set a faction teleport location from the player current position relative to the source ID and target ID. Faction means all of that faction can use this teleporter
* /tt <SourceId> <TargetId> : public teleporter from current position

Example:
- Basis: Akua (Id:1001)
- CV: Akua Orbit (Id:4004)

1. Mark on/in the base a teleporter spot and use the command "/tt 1001 4004"
2. Visit your CV ;-)
3. Mark on/in the CV a teleporter spot and use the command "/tt 4004 1001"

=> Now a teleporter route is set up and can be used from the two marked positions in which the player, standing at the position, places the command /tt in the fraction chat.

HINWEIS: Keep in mind that before you use the teleport commands you need to wait a bit until the Empyrion API registers your player position. Otherwise it is not 100% accurate. ;-)

### Configuration
You can configure the mod in

[Empyrion Directory]\Saves\Games\[SaveGameName]\Mods\EmpyrionTeleporter\TeleporterDB.xml

(will be created with the first start automatically).

* HoldPlayerOnPositionAfterTeleport: Time in seconds where the player will be hold to the teleport position until the structure is loaded (to prevent gravity fall down for example)
* CostsPerTeleporterPosition: Set credit cost for creating a teleport connection point
* CostsPerTeleport: Set credit cost for using the teleporter
* AllowedStructures: Set the allowed structures player can use the teleporter on/in
  - EntityType: BA, CV, SV, HV 
  - FactionGroups: Faction, Player, Alien, Admin

### Is that it?
First of all, and so much fun while teleporting wishes you

ASTIC/TC
