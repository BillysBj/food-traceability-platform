# DEVELOPMENT_PLAN.md
# Food Traceability Platform – Entwicklungsplan

## Verbindlichkeit des Decision Logs

`docs/DECISIONS.md` ist die Source of Truth für explizite Architektur- und Modellentscheidungen. Bei Widersprüchen gilt eine dort als `ENTSCHIEDEN` geführte Entscheidung; als `OFFEN` geführte Entscheidungen dürfen nicht durch Implementierung vorweggenommen werden.

## 1. Arbeitsmodell

```text
Du / Product Owner / Entwickler
        ↓
Claude = Architect + Task Planner + Reviewer
        ↓
Codex = Implementierung + Tests
        ↓
Claude = Review + Abnahme
        ↓
Du = finale Freigabe
```

Claude erstellt kleine, prüfbare Tasks. Codex implementiert nur den aktuellen Scope. Claude prüft Architektur, Security, Datenbank und Tests.

## 2. Task-Status

`PLANNED`, `READY`, `IN_PROGRESS`, `REVIEW`, `CHANGES_REQUIRED`, `APPROVED`, `DONE`, `BLOCKED`

## 3. Standard-Taskformat

Jeder Task enthält: ID, Titel, Ziel, Scope, Out of Scope, Abhängigkeiten, technische Vorgaben, Acceptance Criteria, erforderliche Tests, erwartete Dateien/Module und offene Punkte.

# EPIC 0 – Foundation

- **FND-001** Solution & Repository-Struktur
- **FND-002** PostgreSQL + Docker Compose
- **FND-003** EF Core Foundation & Migrations
- **FND-004** OpenAPI, Problem Details, Logging, Correlation ID, Health Checks
- **FND-005** Unit/Integration/Architecture Test Foundation

Milestone: `M0 – Foundation Ready`

## Hinweis zur tatsächlichen Foundation-Tasknummerierung

Die tatsächliche Umsetzung weicht von der ursprünglichen EPIC-0-Liste ab:

- **FND-001** Solution- und Repository-Struktur
- **FND-002** PostgreSQL via Docker Compose
- **FND-003** EF Core Foundation und erste Migration
- **FND-004** OpenAPI, Problem Details, Logging, Correlation ID, Health Checks
- **FND-005** API Security Baseline (Rate Limiting, CORS, Security Headers)
- **FND-006** Test Foundation (Testcontainers, NetArchTest, Testisolation)
- **CI-001** GitHub-Actions-Pipeline

Grund: FND-004 bündelte im ursprünglichen Plan fünf Themen; Rate Limiting, CORS und Security Headers hatten dort überhaupt keinen eigenen Task, obwohl `AGENTS.md` §38 sie verbindlich fordert.

# EPIC 1 – Identity

- **ID-001** User Domain Model
- **ID-002a** Identity Persistence Foundation
- **ID-002** Roles
- **ID-003** Permissions
- **ID-004** Organization Membership + optional Location Scope
- **ID-005** Authentication
- **ID-006** Permission-based Authorization
- **ID-007** Security & Cross-Tenant Tests

Milestone: `M1 – Identity Ready`

## ID-002a – Identity Persistence Foundation

Bewusst eingeschobener Architektur-Task, im ursprünglichen Plan nicht
vorgesehen. Er etabliert die Persistenzmechanik des ersten Fachmoduls anhand
des bereits vorhandenen User-Domain-Modells aus ID-001.

Begründung: Der erste Modul-DbContext ist eine Weichenstellung für alle zehn
Module. FND-003 hat festgelegt, dass modulspezifische Kontexte eine eigene
Migration-History im jeweiligen Modul-Schema erhalten. Dieses Muster soll
isoliert entstehen und reviewbar sein, statt vermischt mit Rollenlogik in
ID-002.

Scope:
- `IdentityDbContext` in `Modules/Identity/...Infrastructure`
- Schema `identity` mit eigener Migration-History
- EF-Core-Mapping des bestehenden `User` inklusive Value Object `EmailAddress`
- erste modulbezogene Migration, ausschließlich für das heute vorhandene Modell
- Integrationstests gegen PostgreSQL via Testcontainers

Nicht enthalten: Roles, Permissions, Organization Assignments, ASP.NET Core
Identity, Authentifizierung, JWT, Refresh Tokens, API-Endpunkte. Die Migration
nimmt keine künftigen Identity-Tabellen vorweg.

# EPIC 2 – Organizations

- **ORG-001** Organization CRUD
- **ORG-002** Location CRUD
- **ORG-003** Membership Management
- **ORG-004** Tenant Isolation Integration Tests

Milestone: `M2 – Organizations Ready`

# EPIC 3 – Catalog

- **CAT-001** Product Category
- **CAT-002** Unit
- **CAT-003** Product
- **CAT-004** Article/SKU
- **CAT-005** minimale Product Profile Foundation

Milestone: `M3 – Catalog Ready`

# EPIC 4 – Traceability Core

- **TRC-001** Lot Domain Model
- **TRC-002** Lot Persistence, Migration, Constraints, Indizes
- **TRC-003** Create Lot API + Permission + Scope
- **TRC-004** Lot Read/List + Pagination/Filter
- **TRC-005** Event Types
- **TRC-006** Traceability Event Domain Model
- **TRC-007** Event Persistence
- **TRC-008** Create Traceability Event, mehrere Inputs/Outputs
- **TRC-009** Cycle Protection
- **TRC-010** Backward Trace
- **TRC-011** Forward Trace
- **TRC-012** Graph Response Model
- **TRC-013** End-to-End Traceability Tests
- **TRC-014** Mixing Test
- **TRC-015** Split Test
- **TRC-016** Cross-Tenant Traceability Test
- **TRC-017** Performance Baseline

Pflichtconstraint:

```text
UNIQUE (organization_id, lot_number)
```

Pflichttest:

```text
OL-001 → PRESS → OIL-001 → BOTTLE → BOT-001
```

Backward(BOT-001) enthält OIL-001 und OL-001. Forward(OL-001) enthält OIL-001 und BOT-001.

Milestone: `M4 – Traceability Core Proven`

# EPIC 5 – Quality

- **QLT-001** Quality Parameter
- **QLT-002** Sample
- **QLT-003** Lab Result
- **QLT-004** Specification
- **QLT-005** Lot Block
- **QLT-006** Lot Release
- **QLT-007** Blocked Lot Logistics Guard
- **QLT-008** Authorization Tests

Milestone: `M5 – Quality Ready`

# EPIC 6 – Documents

- **DOC-001** Document Metadata
- **DOC-002** Object Storage Abstraction
- **DOC-003** Upload API + Validation
- **DOC-004** Links zu Lot/Sample/Organization/Delivery

Milestone: `M6 – Documents Ready`

# EPIC 7 – Logistics

- **LOG-001** Transport
- **LOG-002** Transport Item
- **LOG-003** Delivery
- **LOG-004** Delivery Item
- **LOG-005** Blocked Lot Guard
- **LOG-006** Forward Trace zeigt Lieferungen/Empfänger

Milestone: `M7 – Logistics Ready`

# EPIC 8 – Public Trace / QR

- **PUB-001** Trace Code / Public Token
- **PUB-002** Public Trace Profile
- **PUB-003** Public Trace API
- **PUB-004** Public Data Security Tests
- **PUB-005** QR Generation

Milestone: `M8 – Public Trace Ready`

# EPIC 9 – Audit

- **AUD-001** Audit Model
- **AUD-002** Audit Coverage für kritische Entities
- **AUD-003** Audit Read API
- **AUD-004** Audit Integrity Tests

Milestone: `M9 – Audit Ready`

# EPIC 10 – Olive Oil Pilot

- **OLV-001** Olive Oil Product Profile
- **OLV-002** Harvest Data
- **OLV-003** Pressing Parameters
- **OLV-004** Oil Yield
- **OLV-005** Olive Oil Quality Configuration
- **OLV-006** kompletter Pilot-End-to-End-Test

Milestone: `M10 – Pilot 1 Backend Complete`

# EPIC 11 – Frontend

- **UI-001** Auth
- **UI-002** Dashboard
- **UI-003** Organizations/Locations
- **UI-004** Users/Roles
- **UI-005** Products/Articles
- **UI-006** Lots
- **UI-007** Lot Detail
- **UI-008** Traceability Event Create
- **UI-009** Traceability Graph
- **UI-010** Quality
- **UI-011** Documents
- **UI-012** Logistics
- **UI-013** QR Management
- **UI-014** Public Consumer Page
- **UI-015** Audit Viewer

Milestone: `M11 – Pilot UI Complete`

# EPIC 12 – Hardening

- **E2E-001** Full Pilot Scenario
- **E2E-002** Authorization Matrix
- **E2E-003** Tenant Isolation für alle Kernendpunkte
- **E2E-004** Traceability Graph Load Test
- **E2E-005** Backup/Restore Test
- **E2E-006** Security Review
- **E2E-007** Release Checklist

Milestone: `M12 – Pilot 1 Release Candidate`

# Spätere Epics

- **EPIC 13 Dairy:** Herd, Milking, Tanks, Cooling, Mixing, Pasteurization, Recipes, Maturation
- **EPIC 14 Meat:** Animal Identity, Slaughter, Carcass, Cutting, Packaging, Cold Chain
- **EPIC 15 Inspection Portal:** Inspections, Inspector, Checklists, Findings, Corrective Actions, Signatures, Reports

Diese Epics starten erst, wenn Pilot 1 stabil ist.

## Branch-Konvention

```text
main
feature/FND-001-solution
feature/ORG-001-organization
feature/TRC-001-lot-domain
feature/TRC-010-backward-trace
```

Ein Task möglichst ein Feature Branch.

## Review Gates

Zwingender Claude-Review bei Foundation, Authorization, Tenant Isolation, Traceability Core, Quality Block/Release, Public Trace Security und E2E.

## Definition of Done

Ein Task ist erst DONE, wenn:
- Code kompiliert
- Tests grün
- Acceptance Criteria erfüllt
- Migrationen valide
- Organization Scope geprüft
- Permissions geprüft
- DB Constraints vorhanden
- OpenAPI aktuell
- keine unerlaubte Modulabhängigkeit
- keine fachliche Regel erfunden
- Dokumentation aktualisiert
- Claude = APPROVED

Nie Scope nebenbei erweitern. Neue Erkenntnisse werden als neue Tasks aufgenommen.


## Swagger-Abnahmekriterium für alle API-Tasks

Für jeden neuen oder geänderten API-Endpunkt gilt:

- Endpoint erscheint in Swagger UI
- Request-DTO ist korrekt beschrieben
- Response-DTO ist korrekt beschrieben
- Statuscodes sind dokumentiert
- Authentifizierung/Authorization ist erkennbar
- JWT/Bearer kann über Swagger UI getestet werden
- `/swagger/v1/swagger.json` bleibt gültig


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

## I18N-001 – Internationalization Foundation

Scope:
- zentrale Frontend-i18n-Infrastruktur
- `en` als Default/Fallback
- `el` als zweite Pilotsprache
- Sprachumschalter
- locale-aware Datum/Zahlen
- Translation-Struktur für übersetzbare Stammdaten
- Public Trace zweisprachig

Acceptance Criteria:
- Englisch und Griechisch funktionieren
- keine sichtbaren Kern-UI-Texte hart codiert
- Fallback auf Englisch funktioniert
- griechische Unicode-Zeichen werden korrekt gespeichert und dargestellt
- neue Sprachen können ohne Schema-Umbau der UI ergänzt werden


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
