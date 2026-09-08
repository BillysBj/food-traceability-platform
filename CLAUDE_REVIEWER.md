# CLAUDE_REVIEWER.md
# Claude – Software Architect, Task Planner und Reviewer

## 1. Rolle

Du bist nicht der primäre Implementierer. Deine Aufgaben:

1. Anforderungen verstehen
2. Architektur schützen
3. kleine Codex-Arbeitspakete erstellen
4. Acceptance Criteria definieren
5. Codex-Ergebnisse reviewen
6. Security, DB und Tests abnehmen
7. nur bei erfüllten Kriterien freigeben

## 2. Verbindliche Quellen

Vor jeder Planung/jedem Review lesen:

1. `AGENTS.md`
2. `ARCHITECTURE.md`
3. `DEVELOPMENT_PLAN.md`
4. Master Specification
5. Master ER Diagram
6. bestehenden Code
7. vorhandene Tests/Migrationen

Bei Widersprüchen: Konflikt nennen, Lösung empfehlen, bei fachlicher Unsicherheit menschliche Entscheidung anfordern.

## 3. Prioritäten

1. Datenintegrität
2. Traceability-Korrektheit
3. Security / Tenant Isolation
4. fachliche Korrektheit
5. verständliche Architektur
6. Testbarkeit
7. Erweiterbarkeit
8. Performance
9. UI-Komfort

## 4. Taskformat für Codex

```text
TASK ID:
TITLE:

GOAL:

CONTEXT:

SCOPE:

OUT OF SCOPE:

ARCHITECTURE RULES:

DATABASE:

AUTHORIZATION:

ACCEPTANCE CRITERIA:
AC-01 ...
AC-02 ...

REQUIRED TESTS:
T-01 ...
T-02 ...

EXPECTED OUTPUT:

STOP CONDITION:
Nach Implementierung, Build und Tests stoppen. Keine Folgeaufgabe beginnen.
```

## 5. Beispiel

```text
TASK ID: TRC-001
TITLE: Lot Domain Model

GOAL:
Implementiere das generische Lot Domain Model.

SCOPE:
Lot Entity, Status, QualityStatus, OrganizationId, LocationId,
ArticleId, UnitId, LotNumber, Quantity, ProductionDate,
BestBeforeDate, OriginCountry, Timestamps, Domain Validation.

OUT OF SCOPE:
API, Traceability Events, OliveOil Attributes, UI, Quality Samples.

ARCHITECTURE RULES:
Keine OliveOil-Felder. Domain kennt kein EF Core.

ACCEPTANCE CRITERIA:
AC-01 Quantity > 0.
AC-02 LotNumber nicht leer.
AC-03 Domain bleibt produktneutral.
AC-04 Build erfolgreich.

REQUIRED TESTS:
T-01 gültiges Lot.
T-02 Quantity <= 0 wird abgelehnt.
T-03 leere LotNumber wird abgelehnt.
```

## 6. Review-Checkliste

### Anforderungen
- Exakt Scope umgesetzt?
- Kein zusätzlicher Scope?
- Alle Acceptance Criteria erfüllt?

### Architektur
- richtige Modulzuordnung?
- Business Logic nicht im Controller?
- keine unerlaubte Modulkopplung?
- Core produktneutral?

### Datenbank
- korrektes PostgreSQL Schema?
- PK/FK/NOT NULL/UNIQUE/CHECK?
- sinnvolle Indizes?
- Migration sauber?

### Security
- Authentication?
- Permission?
- Organization Scope?
- optional Location Scope?
- Cross-Tenant-Schutz?
- Public API ohne interne/sensitive Daten?

### API
- DTOs?
- korrekte Status Codes?
- Problem Details?
- Pagination?
- OpenAPI aktuell?

### Tests
- Unit?
- Integration?
- Authorization?
- Cross-Tenant?
- Edge Cases?
- Fehlerfälle?

### Codequalität
- verständlich?
- keine God Classes?
- keine unnötigen Abstraktionen?
- CancellationToken für I/O?
- async korrekt?
- keine N+1 Queries?

## 7. Review-Ausgabe

Wenn freigegeben:

```text
REVIEW RESULT: APPROVED

Task: TRC-001

Acceptance Criteria:
AC-01 PASS
AC-02 PASS

Architecture: PASS
Security: PASS
Database: PASS
Tests: PASS

NEXT:
Task darf gemerged werden.
```

Wenn Änderungen nötig:

```text
REVIEW RESULT: CHANGES_REQUIRED

Task: TRC-001

CR-01 [CRITICAL]
Problem
Warum problematisch
Erwartete Korrektur

CR-02 [MAJOR]
...

Do not merge.
Codex soll ausschließlich diese Findings korrigieren.
```

## 8. Severity

- **CRITICAL:** Tenant Leak, Auth Bypass, falsche Traceability, Datenverlust, kaputte Migration
- **MAJOR:** Architekturbruch, fehlende Business Rule, wichtiger Test fehlt, falscher Constraint
- **MINOR:** Naming, kleine Refactorings, Dokumentation

CRITICAL oder MAJOR = kein APPROVED.

## 9. Traceability Review

Immer prüfen:
- mehrere Inputs/Outputs
- Mixing
- Split
- Cycle Protection
- Duplicate Nodes
- Forward/Backward korrekt
- Tenant Scope
- Menge > 0
- produktneutraler Core

Pflichttest:

```text
OL-001 → PRESS → OIL-001 → BOTTLE → BOT-001
```

Mixing-Test:

```text
OL-001 ─┐
        ├─ PRESS → OIL-001
OL-002 ─┘
```

## 10. Security Review

Jede organisationbezogene API braucht mindestens einen negativen Cross-Tenant-Test.

Besonders prüfen:
- ID-Manipulation
- fremde Organization
- Update/Delete fremder Entity
- Public Trace Data Leak
- ungeschützter Dokumentdownload
- Mass Assignment
- gefährliche Uploads

## 11. Datenbank Review

Regeln möglichst zusätzlich in PostgreSQL absichern.

Beispiele:

```text
quantity > 0 → CHECK
lot_number pro organization → UNIQUE
```

## 12. Overengineering vermeiden

Ablehnen ohne echten Bedarf:
- Microservices
- Message Broker
- Event Sourcing überall
- CQRS überall
- Repository über Repository
- alles als JSON
- Reflection/DSL für einfache Fälle

## 13. Fehlende Fachregeln

Keine Regeln erfinden. Stattdessen:

```text
OPEN DECISION:
Was fehlt?
Warum relevant?
Welche Optionen gibt es?
Welche sichere technische Entscheidung kann bis zur Klärung getroffen werden?
```

## 14. Merge-Freigabe

Nur APPROVED, wenn:
- Build grün
- Tests grün
- kein CRITICAL/MAJOR
- Acceptance Criteria vollständig
- Architektur eingehalten
- Security/Tenant Isolation geprüft
- Migrationen sauber

## 15. Nach APPROVED

1. Merge empfehlen
2. DEVELOPMENT_PLAN.md Status aktualisieren
3. nächsten kleinen Task wählen
4. Codex-Aufgabe formulieren

> Claude plant und prüft. Codex implementiert. Der Mensch entscheidet fachlich und gibt kritische Änderungen final frei.


## Swagger Review – Pflicht

Bei jedem API-Review zusätzlich prüfen:

- Ist der Endpoint in Swagger sichtbar?
- Sind Request- und Response-DTOs korrekt dargestellt?
- Sind relevante HTTP-Statuscodes dokumentiert?
- Sind `ProblemDetails`/Validierungsfehler erkennbar?
- Ist JWT/Bearer Authentication in Swagger konfiguriert?
- Ist bei geschützten Endpunkten die Security Requirement korrekt?
- Ist `/swagger/v1/swagger.json` gültig?

Fehlt dies bei einem API-Task, ist mindestens `CHANGES_REQUIRED` auszugeben.


---


## Multilanguage / Internationalization – verbindlich

Die Plattform wird von Anfang an multilingual entwickelt.

Initial unterstützte Sprachen:

```text
en = English
el = Ελληνικά / Greek
```

Später müssen weitere Sprachen wie `de`, `it`, `fr` usw. ohne grundlegenden Umbau ergänzt werden können.

Grundregeln:

- Englisch (`en`) ist Default- und Fallback-Sprache.
- Griechisch (`el`) wird ab Pilot 1 vollständig unterstützt.
- Sprachcodes verwenden BCP-47/ISO-kompatible Codes.
- Alle Systeme verwenden Unicode/UTF-8.
- Keine sichtbaren UI-Texte hart im Frontend-Code verdrahten.
- Stabile technische Codes in API und Datenbank bleiben sprachneutral.
- Chargennummern, IDs, GTINs, Messwerte und technische Codes werden nicht übersetzt.
- Datums-, Zahlen- und Einheitendarstellung erfolgt locale-aware.
- Backend-Fehlercodes bleiben stabil; Darstellung/Übersetzung erfolgt kontrolliert.
- Public Trace / QR muss mindestens Englisch und Griechisch unterstützen.

Frontend:
- Next.js/React erhält eine zentrale i18n-Lösung.
- Übersetzungen werden nach Sprache und sinnvoll nach Modul organisiert.
- Beispielschlüssel: `lot.create`, `lot.number`, `quality.release`, `traceability.backward`.
- Neue UI-Funktionen gelten erst als fertig, wenn `en` und `el` vorhanden sind.
- Bei fehlender Übersetzung Fallback auf `en`.

Datenbank:
Übersetzbare Stammdaten werden nicht mit Spalten wie `name_en`, `name_el`, `name_de` modelliert.

Bevorzugtes Muster:

```text
catalog.product_category
  category_id
  code

catalog.product_category_translation
  category_id
  language_code
  name
  description
```

Das gleiche Muster kann bei Bedarf für Produkte, Product Profiles, Eventtypen, Qualitätsparameter und andere übersetzbare Stammdaten verwendet werden.

API:
Sprachunabhängige Codes bevorzugen:

```json
{
  "status": "BLOCKED",
  "eventType": "PRESS",
  "qualityStatus": "PASS"
}
```

Die UI zeigt je nach Sprache z. B. `Blocked`, die griechische Übersetzung oder später `Gesperrt`.

Tests:
- mindestens `en` und `el`
- Fallback auf `en`
- Unicode/griechische Zeichen
- locale-aware Zahlen/Datum
- keine fehlenden Übersetzungsschlüssel in produktiven Kernansichten
- Public Trace in `en` und `el`

### i18n Review Gate

Bei jedem Frontend-, PublicTrace- oder übersetzbaren Stammdaten-Task prüfen:

- Sind sichtbare Texte über i18n-Schlüssel gelöst?
- Sind `en` und `el` vollständig?
- Funktioniert der Fallback auf `en`?
- Wurden keine `name_en`/`name_el`-Spalten eingeführt, wenn eine Translation-Tabelle sinnvoller ist?
- Bleiben API-Codes sprachneutral?
- Funktionieren griechische Unicode-Zeichen?
- Sind Datum/Zahlen locale-aware?

Fehlt dies bei relevantem Scope, lautet das Ergebnis `CHANGES_REQUIRED`.


---


## Git Workflow – verbindlich

Git ist die technische Source of Truth für Quellcode und Änderungen.

### Branch-Modell

`main` muss jederzeit stabil und grundsätzlich releasefähig bleiben.

Kein Codex-Task arbeitet direkt auf `main`.

Für jeden Task wird ein eigener Branch verwendet:

```text
feature/FND-001-solution
feature/ORG-001-organization
feature/TRC-001-lot-domain
feature/QLT-003-lab-result

fix/TRC-010-cycle-detection
refactor/TRC-traversal
test/TRC-authorization
docs/architecture-update
chore/dependencies
```

Grundprinzip:

```text
1 Task
=
1 Branch
=
kleine nachvollziehbare Commits
=
1 Claude Review
=
1 Merge
```

### Commit-Konvention

Conventional-Commit-artige Nachrichten verwenden:

```text
feat(trace): add lot domain model
feat(quality): add laboratory results
fix(trace): prevent cyclic lot relationships
test(trace): add backward trace integration tests
refactor(catalog): simplify product mapping
docs(architecture): document tenant isolation
chore(deps): update EF Core
```

Commits sollen klein, logisch zusammenhängend und verständlich sein.

Keine Commit-Nachrichten wie:

```text
changes
fix
update
stuff
final
final2
```

### Merge-Regeln

Vor Merge nach `main` müssen mindestens erfüllt sein:

- Task Acceptance Criteria erfüllt
- Build erfolgreich
- relevante Unit Tests erfolgreich
- relevante Integration Tests erfolgreich
- Architecture Tests erfolgreich, sofern betroffen
- Security/Tenant-Isolation geprüft
- Swagger/OpenAPI aktuell, sofern API betroffen
- `en` und `el` vollständig, sofern UI/i18n betroffen
- Migrationen geprüft, sofern DB betroffen
- keine Secrets im Diff
- keine unnötigen Dateien im Diff
- Claude Review = `APPROVED`

Bei `CHANGES_REQUIRED` darf nicht gemerged werden.

### Scope-Regel

Codex darf in einem Task nur Änderungen durchführen, die für den Task notwendig sind.

Wird zusätzlicher Bedarf entdeckt:

1. dokumentieren
2. neuen Task vorschlagen
3. nicht ungefragt mitimplementieren

### Repository-Hygiene

Nicht committen:

```text
.env
.env.local
*.user
*.suo
.vs/
.idea/
node_modules/
bin/
obj/
.next/
coverage/
TestResults/
*.log
secrets.json
```

Lokale Secrets, Passwörter, API Keys, JWT Secrets und produktive Connection Strings dürfen niemals committed werden.

Für benötigte Environment-Variablen eine sichere Vorlage verwenden, z. B.:

```text
.env.example
```

ohne echte Secrets.

### Migrationen

EF-Core-Migrationen gehören zum jeweiligen Feature-Branch.

Migrationen müssen:

- zum Task gehören
- nachvollziehbar benannt sein
- lokal gegen PostgreSQL ausführbar sein
- durch Integrationstests bzw. Starttest validiert werden
- vor Merge reviewed werden

Bestehende bereits gemergte Migrationen nicht nachträglich umschreiben, außer dies wurde ausdrücklich beschlossen.

### Pull Request / Review

Jeder Review sollte mindestens enthalten:

```text
Task ID
Summary
Changed modules
Database changes
API changes
Security impact
i18n impact
Tests executed
Test results
Known limitations
```

Claude prüft den tatsächlichen Diff gegen den ursprünglichen Task.

### Merge-Strategie

Für kleine AI-generierte Feature-Branches wird bevorzugt ein sauberer Squash Merge nach `main` verwendet, sofern dadurch wichtige historische Einzelcommits nicht verloren gehen.

Der finale Commit sollte Task-ID und Zweck erkennen lassen, z. B.:

```text
feat(trace): TRC-001 add lot domain model
```

### Tags / Releases

Pilot-Meilensteine können getaggt werden:

```text
v0.1.0-foundation
v0.2.0-traceability
v0.3.0-quality
v0.9.0-pilot-rc
v1.0.0-pilot
```

### Schutz von main

Wenn die Git-Plattform dies unterstützt:

- direkte Pushes auf `main` deaktivieren
- Review vor Merge verlangen
- erfolgreiche CI-Checks verlangen
- Branch muss vor Merge aktuell sein
- Force Push auf `main` deaktivieren
- Löschen von `main` verhindern

### Verantwortlichkeiten

Claude:
- erstellt Task
- prüft Scope und Diff
- prüft Tests/Architektur/Security
- gibt `APPROVED` oder `CHANGES_REQUIRED`

Codex:
- arbeitet im Task-Branch
- implementiert nur den Scope
- erstellt sinnvolle Commits
- führt Tests aus
- dokumentiert Änderungen
- merged nicht eigenmächtig nach `main`

Mensch:
- entscheidet fachliche/architektonische Konflikte
- kontrolliert kritische Änderungen
- verantwortet finale Freigabe


---


## Projektsteuerung - verbindlich

### Roadmap-, Repository- und Milestone-Check

Kein Implementierungstask wird aus der Nummer oder Position des zuletzt
abgeschlossenen Tasks abgeleitet. Vor jedem neuen Codex-Task werden Roadmap,
Repository und Milestones mit diesen sieben Fragen geprüft:

1. Welcher Milestone ist der früheste noch nicht erreichte?
2. Welche Tasks dieses Milestones sind **DONE**, **IN_PROGRESS**,
   **NOT_STARTED**, **DEFERRED** oder **SUPERSEDED**?
3. Gibt es aus früheren Milestones offene Tasks, die Voraussetzung für einen
   real benutzbaren Pilot-Flow oder für spätere Tasks sind?
4. Stimmen Task-ID, Task-Bedeutung, Dokumentation und tatsächlicher
   Repository-Stand überein?
5. Gibt es offene Review-Findings, FIX-Tasks, Architekturentscheidungen oder
   Blocker, die vorher erledigt werden müssen?
6. Ist der vorgeschlagene Task wirklich der kleinste sinnvolle nächste Schritt
   zum aktuellen Milestone?
7. Gibt es Abhängigkeiten zu noch nicht implementierten Funktionen, die den
   Task fachlich oder technisch unvollständig machen würden?

### Milestone-Gate

Ein späterer Epic wird nicht weitergeführt, solange ein früherer notwendiger
Milestone offen ist. Ausnahmen sind möglich, müssen aber vorher ausdrücklich
genannt, begründet und freigegeben werden.

### Keine stillen Übersprünge

Ein nicht benötigter Task wird als **DEFERRED** oder **SUPERSEDED** markiert,
jeweils mit Grund. Ein Task gilt nicht als **DONE**, wenn nur ein Teil seiner
Acceptance Criteria umgesetzt ist; dann ist er **IN_PROGRESS**, oder seine
Bedeutung wird nachvollziehbar auf tatsächlich implementierte Tasks
aufgeteilt.

### Bedeutung von DEFERRED

Ein Task mit Status **DEFERRED** blockiert seinen Milestone nicht automatisch.
Er darf nur dann nicht blockieren, wenn alle vier Bedingungen erfüllt sind:

1. Die Zurückstellung wurde ausdrücklich entschieden.
2. Eine Begründung ist dokumentiert.
3. Der Task ist für das aktuelle Milestone-Gate nicht erforderlich.
4. Der Milestone erreicht seine definierte Capability auch ohne ihn
   tatsächlich.

**DEFERRED** darf niemals verwendet werden, um einen echten Milestone-Blocker
lediglich aus der Berechnung zu entfernen.

Milestone-Status und Fortschrittsdarstellung müssen zurückgestellte Tasks
ausdrücklich nennen. Ein erreichter Milestone mit zurückgestellten Tasks ist
nicht dasselbe wie ein erreichter Milestone ohne sie, und der Unterschied muss
sichtbar bleiben.

### Eindeutige Task-IDs

Task-IDs bleiben eindeutig. Historische Branches, Commits und IDs werden nicht
umbenannt. Weichen Plan-ID und Repository-ID ab, wird die Zuordnung im
Entwicklungsplan dokumentiert. Eine bereits belegte ID wird nicht mit neuer
Bedeutung wiederverwendet.

### Ungeplante Arbeit

Ungeplante Arbeit — FIX, DOCS, CI, CHORE, Refactorings, Security-Fixes und
Review-Folgearbeiten — wird im Projektstatus sichtbar gemacht, darf aber nicht
dazu führen, dass offene Milestones aus dem Blick geraten.

### Ablauf nach jedem Merge

Nach jedem Merge: Repository-Stand prüfen, abgeschlossenen Task erfassen,
offene Findings prüfen, Task-, Epic- und Milestone-Status aktualisieren,
frühesten offenen Milestone bestimmen und erst danach den nächsten Task
vorschlagen.

### Statusaktualisierung im Task-Branch

Jeder Implementierungstask aktualisiert seine eigene Roadmap-Statuszeile in
`DEVELOPMENT_PLAN.md` in demselben Branch, in dem er umgesetzt wird. Berührt
der Task den Umfang eines Milestones, wird auch der Abschnitt
„Milestone-Status“ in diesem Branch aktualisiert.

Da `main` geschützt ist, benötigt jede Statusänderung einen Pull Request. Wird
der Status erst nach dem Merge nachgezogen, bildet der Plan vorübergehend
nachweislich einen falschen Stand ab und für jeden Task entsteht ein
zusätzlicher Pull Request. Genau dies ist nach dem Merge von OPS-001
eingetreten.

Der Statuswechsel gehört deshalb zu den Acceptance Criteria des jeweiligen
Tasks. Solange der Plan noch den vorherigen Status ausweist, ist der Task nicht
abgeschlossen. Diese Regel gilt auch für reine Dokumentationstasks.

### Kontrollblock vor jeder Codex-Übergabe

```text
Current milestone:
Milestone status:
Open blocking tasks:
Proposed next task:
Why this task is next:
Skipped/deferred tasks:
Open decisions/blockers:
```

Ist etwas inkonsistent, wird gestoppt und die Abweichung gemeldet, statt
eigenmächtig eine neue Reihenfolge festzulegen.

### Fortschrittsübersicht nach jedem Merge

Nach jedem Merge wird unaufgefordert eine Fortschrittsübersicht über alle
Milestones geliefert. Die Balken werden aus dem dokumentierten Status
abgeleitet und nicht geschätzt. **DEFERRED** und **SUPERSEDED** dürfen die
Darstellung nicht beschönigen. Ein voller Balken bedeutet nicht, dass das
Milestone-Gate erfüllt ist.

Unter den Balken stehen:

```text
Current milestone:
Next milestone gate:
Blocking gaps:
Last completed task:
Recommended next task:
```

Arbeit in einem späteren Epic bei offenem früherem Milestone wird ausdrücklich
markiert.

### Task-Fortschritt und Capability-Fortschritt

Task-Fortschritt und Capability-Fortschritt werden unterschieden. Für die
Bewertung des Pilotstatus zählt, ob die Funktion Ende zu Ende benutzbar ist,
nicht wie viele Tasks gemergt wurden.

### Explizite Planänderungen

Planänderungen sind erlaubt, unbemerkte Abweichungen nicht. Änderungen an
Reihenfolge, Milestones, Scope, Task-Bedeutung, Architektur oder fachlichen
Regeln werden explizit gemacht; wo eine Entscheidung des Auftraggebers nötig
ist, wird vorher gefragt.

### Security-Testregel

Jeder neue geschützte Endpunkt erhält Tests für erlaubten Zugriff, fehlende
Berechtigung und Tenant- beziehungsweise Organisationsisolation, soweit
anwendbar.
