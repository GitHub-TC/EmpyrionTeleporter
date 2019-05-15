# Empyrion Teleporter
## Installation
1. Download der aktuellen ZIP datei von https://github.com/GitHub-TC/EmpyrionTeleporter/releases
1. Upload der Datei im EWA (EmpyrionWebAccess) MOD oder händische installation mit dem ModLoader aus https://github.com/GitHub-TC/EmpyrionModHost

Demo: https://empyriononline.com/threads/mod-empyrionteleporter.39200/

### Wo für ist das?

Ein Teleport zwischen zwei vorher eingerichteten Positionen. Dabei muss der Spieler zuvor an BEIDEN Orten gewesen sein und dort die Teleporterposition relativ zu einer Struktur festgelegt haben.

#### Anleitung zum Einrichten eines virtuellen Teleporters:
Befehle über die Fraktionstextleiste eingeben (Fraktionschat: entspricht der ; Semikolon oder Kommataste)
* Als erstes stelle dich an die Stelle von wo du den ersten Teleporterpunkt setzen möchtest... z.B. in deiner Base. Wie man diesen Platz einrichtet ist völlig egal. Am besten ein LED Pad mit Aufschrift teleport nach xy.... und ihn vielleicht farblich markieren damit man ihn wiederfindet.
Dann gibst du folgenden Befehl ein: \tt 1234567 7654321 und Entertaste wobei die erste Zahl der ID deiner Basis entspricht und nach einer Leertaste die ID deines Zieles entspricht. Dies kann ein Raumschiff oder eine weitere Basis sein.
So der erste Telepunkt ist nun gesetzt.
* Nun begebe dich zu deinem Zielort. Stelle dort auch ein Display auf und beschrifte es dem entsprechen z.B. Teleport zur Basis…. und auch hier solltest du ihn vielleicht farblich markieren damit man ihn wiederfindet.
Dann gib folgenden Befehl ein: \tt 7654321 1234567
Also die ID Nummern in genau umgekehrter Reihenfolge

Und schon ist der Teleporter fertig eingerichtet und du kannst ihn mit \tt benutzen.<br/>
Viel Spaß beim Teleportieren....

#### Hilfe

Die Kommandos funktionieren NUR im Fraktionschat!
* \tt help : Zeigt die Kommandos der Mod an

#### Teleport

* \tt => Teleport von dieser (vorher eingerichteten) Position zum Ziel durchführen
* \tt help => Liste der Kommandos
* \tt back => Falls ein Teleport schiefgegenen sein sollte kann sich der Spieler hiermit zu der Position VOR dem Teleport zurück teleportieren lassen
* \tt delete <SourceId> <TargetId> => Löscht alle Teleporterouten von <SourceId> und nach <TargetId> - wenn <TargetId> = 0 ist werden ALLE Routen von <SourceId> gelöscht
* \tt list <Id> => Listet alle Teleporterouten von und nach <Id> auf
* \tt listall => Listet alle Teleporterouten auf (nur ab Moderator erlaubt)
* \tt cleanup => Löscht alle Teleporterrouten die zu gelöschten Strukturen führen (nur ab Moderator erlaubt)
* \tt private <SourceId> <TargetId> => Privaten Teleportort von der aktuellen Spielerposition relativ zur <SourceId> nach <TargetId> einrichten der nur für den Spieler nutzbar ist.
* \tt faction <SourceId> <TargetId> => Fraktions Teleportort von der aktuellen Spielerposition relativ zur <SourceId> nach <TargetId> einrichten der nur für die Fraktion nutzbar ist.
* \tt allies <SourceId> <TargetId> => Fraktions/Allierte Teleportort von der aktuellen Spielerposition relativ zur <SourceId> nach <TargetId> einrichten der nur für die Fraktion und deren Allierte nutzbar ist.
* \tt <SourceId> <TargetId> => Öffentlichen Teleportort von der aktuellen Spielerposition relativ zur <SourceId> nach <TargetId> einrichten.

Beispiel:
- Basis: Akua (Id:1001)
- CV: Akua Orbit (Id:4004)

1. Auf/Bei der Basis die Position des Spielers markieren (Textur, LCD, Farbe,...) und das Kommando "\tt 1001 4004" aufrufen
2. Zum CV reisen (noch manuell) ;-)
3. Auf/Bei/In dem CV die Position des Spielers markieren (Textur, LCD, Farbe,...) und das Kommando "\tt 4004 1001" aufrufen

=> Nun ist eine Teleporterroute eingerichtet und kann von den beiden markierten Positionen aus benutzt werden in dem der Spieler, an der Position stehend, im Fraktionschat den Befehl \tt absetzt.

HINWEIS: Da Empyrion es mit den Positionen beim Teleport nicht so genau nimmt sollte der Raum um einen herum ausreichend Platz bieten ;-)

### Konfiguration
Eine Konfiguration kann man in der Datei (wird beim ersten Start automatisch erstellt)

[Empyrion Directory]\Saves\Games\[SaveGameName]\Mods\EmpyrionTeleporter\TeleporterDB.xml

vornehmen.

* HoldPlayerOnPositionAfterTeleport: Zeit in Sekunden die ein Spieler nach dem Teleport auf Position gehalten wird bis die Strukturen (hoffentlich) nachgeladen sind
* PreparePlayerForTeleport: Zeit in Sekunden die der Spieler sich auf den Teleport vorbereiten kann (Chat schließen, Finger auf die Jetpacktaste und die Leertaste legen... ;-) )
* CostsPerTeleporterPosition: Creditkosten für das Setzen eines Teleporterpunktes
* CostsPerTeleport: Creditkosten für das Benutzen eines Teleporterpunktes
* AllowedStructures: Liste der erlaubten Strukturen für Teleporterpunkte hierbei sind folgende Werte erlaubt
  - EntityType: BA, CV, SV, HV 
  - FactionGroups: Faction, Player, Alien, Admin
* ForbiddenPlayfields: Liste der Playfield in oder zu denen kein Teleporten oder Erstellen eines Teleporter erlaubt ist
  - Beispieleintrag: <string>Akua</string>

### Was kommt noch?
Zunächst erstmal und damit viel Spaß beim Teleportieren wünscht euch

ASTIC/TC

***

English-Version:

---

## Installation
1. Download the current ZIP file from https://github.com/GitHub-TC/EmpyrionTeleporter/releases
1. Upload the file in the EWA (EmpyrionWebAccess) MOD or manual installation with the ModLoader from https://github.com/GitHub-TC/EmpyrionModHost
You can find a compiled DLL version in the EmpyrionTeleporter/bin directory if you do not want to check and compile the mod myself ;-)

Demo: https://empyriononline.com/threads/mod-empyrionteleporter.39200/

### What is this?

A MOD which can be used to teleport players to previously configured positions. It's required that the player must visited the places before and the teleport positions got binded to the structure.

#### How to set up a virtual teleporter:
Enter commands via the fraction text bar (faction chat: corresponds to the semicolon or comma key)
* First, take a look at where you want to put the first teleporter point ... e.g. in your base. How to set up this place does not matter. Best an LED pad with inscription teleport to xy .... and maybe mark him in color so that you can find him again.
Then enter the following command: / tt 1234567 7654321 and enter key where the first number corresponds to the ID of your base and after a space bar corresponds to the ID of your target. This can be a spaceship or another base.
So the first telepoint is now set.
* Now go to your destination. Place a display there as well and label it according to e.g. Teleport to the base .... And here you should also mark it in color so that you will find it again.
Then enter the following command: / tt 7654321 1234567
So the ID numbers in exactly the reverse order

And the teleporter is already set up and you can use it with / tt.<br/>
Have fun teleporting ....

#### Help

All commands only work in faction chat!
* \tt help : show useful information and commands about the MOD

#### Teleport

* \tt : teleport the player from previously configured position to the destination
* \tt help : show all commands
* \tt back : if a teleport failed, the player can get back to the position he came from
* \tt delete <SourceId> <TargetId> => Deletes all teleport routes from <SourceId> and after <TargetId> - when <TargetId> = 0 ALL routes from <SourceId> will be deleted
* \tt list <Id> : show all teleport connections from and to this ID
* \tt listall : show all teleport connections (only Moderators can use it)
* \tt cleanup : remove all teleport connections to deleted structures (only Moderators can use it)
* \tt private <SourceId> <TargetId> : set a private teleport location from the player current position relative to the source ID and target ID. Private means only that player can use this teleporter
* \tt faction <SourceId> <TargetId> : set a faction teleport location from the player current position relative to the source ID and target ID. Faction means all of that faction can use this teleporter
* \tt allies <SourceId> <TargetId> : set a faction/allies teleport location from the player current position relative to the source ID and target ID. Faction means all of that faction and allies can use this teleporter
* \tt <SourceId> <TargetId> : public teleporter from current position

Example:
- Basis: Akua (Id:1001)
- CV: Akua Orbit (Id:4004)

1. Mark on/in the base a teleporter spot and use the command "\tt 1001 4004"
2. Visit your CV ;-)
3. Mark on/in the CV a teleporter spot and use the command "\tt 4004 1001"

=> Now a teleporter route is set up and can be used from the two marked positions in which the player, standing at the position, places the command \tt in the fraction chat.

HINWEIS: Keep in mind that before you use the teleport commands you need to wait a bit until the Empyrion API registers your player position. Otherwise it is not 100% accurate. ;-)

### Configuration
You can configure the mod in

[Empyrion Directory]\Saves\Games\[SaveGameName]\Mods\EmpyrionTeleporter\TeleporterDB.xml

(will be created with the first start automatically).

* HoldPlayerOnPositionAfterTeleport: Time in seconds where the player will be hold to the teleport position until the structure is loaded (to prevent gravity fall down for example)
* PreparePlayerForTeleport: Time in seconds the player can prepare for the teleport (close the chat, put his finger on the Jetpack key and the space bar ... ;-))
* CostsPerTeleporterPosition: Set credit cost for creating a teleport connection point
* CostsPerTeleport: Set credit cost for using the teleporter
* AllowedStructures: Set the allowed structures player can use the teleporter on/in
  - EntityType: BA, CV, SV, HV 
  - FactionGroups: Faction, Player, Alien, Admin
* ForbiddenPlayfields: List of playfields in or to which no teleporting or teleporter creation is allowed
  - Example entry: <string> Akua </ string>

### Is that it?
First of all, and so much fun while teleporting wishes you

ASTIC/TC
