# Empyrion Teleporter
## FAQ

Eine übersetzte Version findet ihr im EmpyrionTeleporter/bin Verzeichnis falls ihr die Mod nicht selber prüfen und compilieren wollt ;-)

### What is this?

Ein Teleport zwischen zwei vorher eingerichteten Positionen. Dabei muss der Spieler zuvor an BEIDEN Orten gewesen sein und dort die Teleporterposition relativ zu einer Struktur festgelegt haben.

#### What are all the commands?

Die Kommandos funktionieren NUR im Fraktionschat!

#### Help

* /tt help : Zeigt die Kommandos der Mod an

#### Teleport

* /tt => Teleport von dieser (vorher eingerichteten) Position zum Ziel durchführen
* /tt help => Liste der Kommandos
* /tt back => Falls ein Teleport schiefgegenen sein sollte kann sich der Spieler hiermit zu der Position VOR dem Teleport zurück teleportieren lassen
* /tt delete <Id> => Löscht alle Teleporterouten von und nach <Id>
* /tt list <Id> => Listet alle Teleporterouten von und nach <Id> auf
* /tt listall => Listet alle Teleporterouten auf (nur ab Moderator erlaubt)
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

### Is that it?
Zunächst erstmal und damit viel Spaß beim Teleportieren wünscht euch

ASTIC/TC