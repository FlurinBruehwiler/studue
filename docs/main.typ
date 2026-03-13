#set text(lang: "de", region: "ch")

= Projektskizze

== Ausgangslage

Das effiziente Verwalten von Abgabeterminen und Prüfungsdaten ist eine zentrale Herausforderung im Studienalltag. Zwar existieren bereits diverse öffentliche Kalender, doch fehlt es diesen oft an einer zentralen Übersicht und klassenspezifischen Features. Um das ständige Nachfragen im Klassenchat und an den Schultagen zu beenden und eine verlässliche Informationsquelle zu schaffen, schlagen wir ein Terminierungs-Tool vor, das alle Aufgaben und Fristen an einem Ort für unsere Klasse bündelt.

== Idee

Wir schlagen vor, ein eintragbasiertes Terminierungs-Tool für Abgaben im Studienalltag zu entwickeln.

Aus den verschiedenen Modulen des Studiums ist nicht übersichtlich, welche Aufgaben an welchen Tagen erledigt sein müssen, was die ohnehin schwierige Zeitplanung komplexer macht. Das Terminierungs-Tool sammelt die von den Benutzern erfassten Abgabedaten und stellt sie übersichtlich dar, sodass keine Abgabe vergessen geht.

#figure(
  image("MainSite.png", width: 80%),
  caption: [
    Hauptseite der Applikation mit Übersicht über die anstehenden Aufgaben
  ],
)

#figure(
  image("Details.png", width: 80%),
  caption: [
    Detailansicht einer Aufgabe
  ],
)

== Kundennutzen

Das Tool wird von Klassenmitgliedern verwaltet, so dass die Abgabetermine korrekt und vollständig im Tool aufgezeigt werden. Der Nutzer hat dadurch keinen Eigenaufwand und kann alle nötigen Informationen zu seinen Abgaben an einer Stelle abgreifen. Somit muss der Nutzer nicht mehr jede Modulseite nach offenen Abgabeterminen durchforsten. Die Einträge im Tool besitzen alle benötigten Informationen, um eine Aufgabe fertigstellen zu können.

== Stand der Technik / Konkurrenzanalyse

Es gibt verschiedene Tools, die ähnliche Funktionen abdecken, wie das unsere Applikation tun soll. Bei jedem Konkurrenten fehlt jedoch ein wichtiger Aspekt, der die Tools für unseren Zweck unbrauchbar macht.

Eine mögliche Alternative ist ein gemeinsamer Kalender, den alle Beteiligten bearbeiten können, beispielsweise Google Calendar. Problematisch daran ist jedoch, dass Änderungen nur eingeschränkt nachvollziehbar sind, also nicht immer klar ersichtlich ist, wer wann was geändert hat. Zudem setzt die Nutzung einen eigenen Account voraus.

Die Einträge auf dem MyZHAW-Portal sind nicht vollständig und zeigen nicht immer auf den korrekten Eintrag. Als Beispiel können wir diese Abgabe des Entwurfs zur Projektskizze nehmen, die Stand heute nicht auf MyZHAW zu finden ist @noauthor_myzhaw_nodate.

Die Moodle-Termine sind auch nicht vollständig und enthalten nur Einträge, die über Moodle abgegeben werden. Aussen vor gelassen werden Aufträge, die im Unterricht besprochen wurden oder nur in den Folien sichtbar sind oder auf anderen Plattformen abgegeben werden müssen. Wie bei MyZHAW erscheint auf der Moodle-Plattform kein Eintrag für die Abgabe heute @noauthor_dashboard_nodate.

== Kontextablauf

Die Anwender der Applikation sind Studenten der Klasse IT25ta_WIN. Sie treten gegenüber dem System in zwei Rollen auf: als Autor, der neue Aufgaben und Abgaben erfasst oder bestehende Einträge ergänzt und korrigiert, und als Leser, der sich über anstehende Termine informiert. Das Produkt besteht im Kern aus einem JavaFX-Client, einem zentralen Java-Server und einer Speicherung der Daten in einer Datei.

Anwendungsfall Autor:
Ein Student der Klasse IT25ta_WIN besucht eine Vorlesung an der ZHAW. Dort wird eine neue Abgabe angekündigt, die in der folgenden Woche fällig ist. Damit diese Information nicht verloren geht, erfasst der Student die Aufgabe im System. Da er zur Rolle des Autors gehört, kann er den neuen Eintrag anlegen und die relevanten Angaben wie Modul, Fälligkeitsdatum, Titel, Bemerkung und Verbindlichkeit festhalten. Nach dem Speichern steht die Aufgabe allen Mitgliedern der Klasse zur Verfügung. Später bemerkt derselbe Student, dass bei einer bereits erfassten Aufgabe ein Schreibfehler vorhanden ist. Er ruft den bestehenden Eintrag erneut auf, prüft die hinterlegten Informationen und korrigiert den Fehler. Dabei bleibt nachvollziehbar, von wem der Eintrag ursprünglich erstellt und zuletzt bearbeitet wurde. Auf diese Weise entsteht im Verlauf des Semesters eine gemeinsame, laufend aktualisierte Übersicht über alle anstehenden Aufgaben und Abgaben.

Anwendungsfall Leser:
Am Sonntagmorgen bereitet sich eine Studentin auf die kommende Schulwoche vor. Sie möchte sich einen schnellen Überblick darüber verschaffen, welche Aufgaben in den nächsten Tagen anstehen und welche davon obligatorisch sind. Dazu nutzt sie die Anwendung in der Rolle der Leserin. Ohne selbst Inhalte zu verändern, kann sie die erfassten Abgaben einsehen und nach Modul oder Verbindlichkeit eingrenzen. So erkennt sie rasch, welche Arbeiten zuerst erledigt werden müssen und bei welchen Modulen in den nächsten Wochen besonderer Handlungsbedarf besteht. Der Nutzen der Anwendung liegt für sie darin, dass die verstreuten Informationen aus Unterricht, Moodle, Folien und anderen Quellen an einem Ort zusammengeführt werden und dadurch eine verlässliche Planungsgrundlage für den Studienalltag entsteht.


== Weitere Anforderungen

Funktionale Anforderungen:
- Für die Leseansicht wird kein Login benötigt
- Um die Aufgaben zu editieren, können sich die Studenten mit ihrem `github.zhaw.ch`-Login anmelden (OAuth).
- Autoren können neue Aufgaben mit Modul, Titel, Fälligkeitsdatum, Bemerkung und Verbindlichkeit erfassen.
- Bereits bestehende Einträge können von berechtigten Nutzern korrigiert und ergänzt werden.
- Die erfassten Aufgaben können nach Modul, Zeitraum und obligatorischen beziehungsweise optionalen Abgaben gefiltert werden.
- Zu jedem Eintrag soll ersichtlich sein, wer ihn erstellt und zuletzt bearbeitet hat.

Nicht funktionale Anforderungen:
- Die Anwendung soll auf den gängigen Betriebssystemen nutzbar sein.
- Die Antwortzeit für typische Abfragen, wie das Anzeigen der anstehenden Aufgaben, soll im Normalfall unter zwei Sekunden liegen.
- Die gespeicherten Daten sollen regelmässig gesichert werden, damit bei einem technischen Ausfall keine wichtigen Informationen verloren gehen.

Zukünftige Erweiterungen:
Der Prototyp besteht aus einem JavaFX-Client und einem Java-Server, die mittels TCP kommunizieren. In Zukunft kann ein zweiter Web-Client programmiert werden, welcher es Nutzern erlaubt, die Applikation direkt von einem Browser aus zu verwenden. Ausserdem kann die Nutzung auf weitere Klassen innerhalb der ZHAW erweitert werden.

== Ressourcen

Für unser Projekt werden wir einen Server benötigen, welcher mit dem JavaFX-Client kommuniziert. Kenntnisse in den folgenden Technologien werden dafür vonnöten sein: Java, JavaFX und TCP.

In den von uns eingesetzten Technologien besitzen alle Teammitglieder ausreichende Grundkenntnisse.

Das Team besteht aus einem Projektleiter, Dennis, der den Überblick über die Abgaben und die Aufgaben behält. Mark arbeitet schon länger mit Java und kann Auskunft geben. Flurin ist gut versiert im Networking. Christian übernimmt die Rolle des Nutzers und testet das Tool auf Fehler und Anfälligkeiten.
Wir gehen davon aus, dass jedes Teammitglied 4 Stunden in der Schule und 4 Stunden in der Freizeit dafür aufbringen wird. Über diese 6 Wochen sind das ungefähr 200 Arbeitsstunden, was ausreichen sollte.


== Risiken

Es gibt bereits persönliche und von der ZHAW existierende Kalender, welche zum Verwalten von Terminen genutzt werden können, wobei diese entweder schlecht geteilt oder nicht angepasst werden können.

Durch eine schlechte Pflege des Kalenders kann dieser zudem seine Relevanz verlieren, sodass Benutzer wieder auf andere Alternativen abspringen. Da bei der manuellen Erstellung von Einträgen durch die Benutzer stets die Gefahr besteht, Termine falsch einzutragen, muss von den Mitnutzern eine gewisse Kontrolle erwartet werden, was in der Praxis jedoch schwierig durchzuführen ist.

== Wirtschaftlichkeit

Für die Entwicklung rechnen wir mit rund 200 Arbeitsstunden über 4 Personen. Bei einem angenommenen Stundenansatz von 30.- CHF pro Person ergeben sich daraus theoretische Personalkosten von rund 6000.- CHF. Da das Projekt jedoch im Rahmen eines Moduls umgesetzt wird, fallen diese Kosten nicht direkt als reale Ausgaben an, zeigen aber den ungefähren Entwicklungsaufwand des Produkts.

Für den Betrieb kann voraussichtlich bestehende Server-Infrastruktur aus dem Team verwendet werden, sodass kurzfristig keine hohen Hosting-Kosten zu erwarten sind. Selbst wenn später ein separates Hosting nötig werden würde, gehen wir für ein kleines studentisches Projekt von tiefen jährlichen Betriebskosten aus.

Da das Projekt als Open-Source-Projekt gedacht ist, steht die Gewinnerzielung nicht im Vordergrund. Realistische direkte Einnahmen sind in den ersten Jahren deshalb gering. Für die nächsten fünf Jahre rechnen wir im konservativen Fall mit keinen festen Einnahmen, höchstens mit kleinen freiwilligen Beiträgen oder einer Unterstützung durch Organisationen wie ALIAS. Damit lägen die Einnahmen vermutlich in einem dreistelligen Bereich. Würde die ALIAS zusätzlich die Betriebskosten übernehmen, könnte das Projekt praktisch ohne laufende Kosten weitergeführt werden.

Aus rein finanzieller Sicht erzielt das Projekt damit kurzfristig kaum nennenswerten Gewinn. Der wirtschaftliche Nutzen liegt vielmehr indirekt in der Zeitersparnis, der besseren Übersicht über Abgaben und dem möglichen langfristigen Mehrwert für spätere Klassen. Falls das Projekt erfolgreich übernommen und weiterentwickelt wird, kann der Nutzen für die Studierenden steigen, auch wenn kein klassisches gewinnorientiertes Geschäftsmodell dahintersteht.

#bibliography("Ressourcen.bib", title: "Ressourcen", style: "ieee")
