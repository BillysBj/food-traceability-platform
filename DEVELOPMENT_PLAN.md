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

Diese Werte beschreiben den Bearbeitungslebenszyklus eines Tasks während seiner
Umsetzung und seines Reviews. Sie sind vom nachfolgenden Roadmap-Status zu
unterscheiden, der den abgeglichenen Stand des Entwicklungsplans wiedergibt.

## Roadmap-Statusvokabular

- **DONE** — Umgesetzt, reviewt, nach `main` gemerged.
- **IN_PROGRESS** — Teilweise umgesetzt. Der Plan-Task ist inhaltlich breiter als das, was gebaut wurde.
- **NOT_STARTED** — Nicht begonnen.
- **DEFERRED** — Bewusst zurückgestellt. Braucht immer einen Grund.
- **SUPERSEDED** — Durch einen anders zugeschnittenen Task ersetzt. Braucht immer einen Verweis auf den ersetzenden Task.

## Plan-Id und Repository-Id

Die IDs in den Epic-Listen sind **Plan-IDs**. Die IDs in Branchnamen, Commits
und Pull Requests sind **Repository-IDs**. Bei Foundation, Identity und Catalog
weichen beide teilweise voneinander ab. Wer einen Task sucht, muss deshalb
zuerst klären, ob eine Plan-ID oder eine Repository-ID vorliegt. Die jeweilige
Repository-ID wird in den Epic-Listen überall dort genannt, wo sie von der
Plan-ID abweicht oder dieselbe ID im Repository einen anderen Inhalt bezeichnet.

Historische Branches, Commits und Pull Requests werden nicht umbenannt. Die
Abweichungen werden dokumentiert und nicht rückwirkend bereinigt.

## Eingeschobene Tasks

Aufgeführt sind alle nach `main` gemergten Tasks, die im ursprünglichen
Entwicklungsplan keinen eigenen Eintrag hatten. Reine Umnummerierungen sowie die
Aufteilung eines geplanten Tasks auf mehrere Repository-Tasks – etwa ID-005 in
ID-005a und ID-005b – gelten nicht als eingeschoben; sie sind in den Epic-Listen
beim jeweiligen Plan-Task vermerkt.

- **FND-005** (Repository-ID) API Security Baseline — **Roadmap-Status: DONE** — Zweck: Rate Limiting, CORS und Security Headers; Begründung und Einordnung siehe Abschnitt „Hinweis zur tatsächlichen Foundation-Tasknummerierung“.
- **ID-002a** Identity Persistence Foundation — **Roadmap-Status: DONE** — Zweck: Identity-Domainmodell und -Persistenz etablieren; Begründung siehe Abschnitt „ID-002a – Identity Persistence Foundation“ in EPIC 1.
- **ID-003a** Role-Permission-Zuordnung — **Roadmap-Status: DONE** — Zweck: Rollen und Permissions über `identity.role_permission` verbinden; Begründung siehe Abschnitt „ID-003a – Role-Permission-Zuordnung“ in EPIC 1.
- **ORG-001a** Organization- und Location-Persistence Foundation — **Roadmap-Status: DONE** — Zweck: Domainmodell und Persistenz für Organisationen und Standorte etablieren; Begründung siehe Abschnitt „ORG-001a – Organization- und Location-Persistence Foundation“ in EPIC 2.
- **CI-001** GitHub-Actions-Pipeline — **Roadmap-Status: DONE**
- **DOCS-001** API-Pfade auf `/api/v1` angeglichen — **Roadmap-Status: DONE**
- **DOCS-002** kanonischen Decision Log eingeführt und Dokumente angeglichen — **Roadmap-Status: DONE**
- **DOCS-003** Identity-Scope und Token-Parameter festgehalten — **Roadmap-Status: DONE**
- **DOCS-004** Organization Context in Routen festgehalten (D-26) — **Roadmap-Status: DONE**
- **DOCS-005** D-27 Platform Scope und Routenbeispiele korrigiert — **Roadmap-Status: DONE**
- **DOCS-006** D-28 bis D-30 Traceability-Kernentscheidungen festgehalten — **Roadmap-Status: DONE**
- **DOCS-007** D-32 und D-33 Mengen- und Einheitenentscheidungen festgehalten — **Roadmap-Status: DONE**
- **DOCS-008** ER-Diagramm und README mit dem Implementierungsstand synchronisiert — **Roadmap-Status: DONE**
- **DOCS-009** Folgebefunde aus dem TRC-003-Review als FIX-007 und FIX-008 im Backlog erfasst — **Roadmap-Status: DONE**
- **FIX-001** Readiness-Check meldet `unhealthy`, statt zu scheitern — **Roadmap-Status: DONE**
- **FIX-002** Exception-Details erreichen das Log — **Roadmap-Status: DONE**
- **FIX-003** ASP.NET-Core-Abhängigkeitsgrenze verengt — **Roadmap-Status: DONE**
- **FIX-004** einheitliche Problem Details über alle Fehlerantworten — **Roadmap-Status: DONE**
- **FIX-005** Rate-Limit-Verhalten statt Werte abgedeckt — **Roadmap-Status: DONE**

FIX-006, FIX-007 und FIX-008 sind noch nicht umgesetzt; sie bleiben
ausschließlich in den bestehenden Backlog-Einträgen dieses Dokuments und werden
hier nicht dupliziert.

## 3. Standard-Taskformat

Jeder Task enthält: ID, Titel, Ziel, Scope, Out of Scope, Abhängigkeiten, technische Vorgaben, Acceptance Criteria, erforderliche Tests, erwartete Dateien/Module und offene Punkte.

# EPIC 0 – Foundation

- **FND-001** Solution & Repository-Struktur — **Roadmap-Status: DONE**
- **FND-002** PostgreSQL + Docker Compose — **Roadmap-Status: DONE**
- **FND-003** EF Core Foundation & Migrations — **Roadmap-Status: DONE**
- **FND-004** OpenAPI, Problem Details, Logging, Correlation ID, Health Checks — **Roadmap-Status: DONE**
- **FND-005** (Plan-ID) Unit/Integration/Architecture Test Foundation — **Roadmap-Status: SUPERSEDED** — **Ersetzt durch:** Repository-Task **FND-006**.

Milestone: `M0 – Foundation Ready`

## Hinweis zur tatsächlichen Foundation-Tasknummerierung

Die tatsächliche Umsetzung weicht von der ursprünglichen EPIC-0-Liste ab:

- **FND-001** Solution- und Repository-Struktur — **Roadmap-Status: DONE**
- **FND-002** PostgreSQL via Docker Compose — **Roadmap-Status: DONE**
- **FND-003** EF Core Foundation und erste Migration — **Roadmap-Status: DONE**
- **FND-004** OpenAPI, Problem Details, Logging, Correlation ID, Health Checks — **Roadmap-Status: DONE**
- **FND-005** (Repository-ID) API Security Baseline (Rate Limiting, CORS, Security Headers) — **Roadmap-Status: DONE**
- **FND-006** (Repository-ID) Test Foundation (Testcontainers, NetArchTest, Testisolation) — **Roadmap-Status: DONE**
- **CI-001** GitHub-Actions-Pipeline — **Roadmap-Status: DONE**

Grund: FND-004 bündelte im ursprünglichen Plan fünf Themen; Rate Limiting, CORS und Security Headers hatten dort überhaupt keinen eigenen Task, obwohl `AGENTS.md` §38 sie verbindlich fordert.

# EPIC 1 – Identity

- **ID-001** User Domain Model — **Roadmap-Status: DONE**
- **ID-002a** Identity Persistence Foundation (eingeschoben) — **Roadmap-Status: DONE**
- **ID-002** Roles — **Roadmap-Status: DONE**
- **ID-003** Permissions — **Roadmap-Status: DONE**
- **ID-003a** Role-Permission-Zuordnung (eingeschoben) — **Roadmap-Status: DONE**
- **ID-004** Organization Membership + optional Location Scope — **Roadmap-Status: DONE**
- **ID-005** (Plan-ID) Authentication — **Roadmap-Status: DONE** — ausgeliefert als Repository-Tasks **ID-005a** (Credential- und Refresh-Token-Persistenz) und **ID-005b** (Authentication-Endpunkte).
- **ID-006** Permission-based Authorization — **Roadmap-Status: DONE**
- **OPS-001** Initial Platform Administrator Bootstrap — **Roadmap-Status: DONE**
- **ID-008** User Management — **Roadmap-Status: DONE**
- **ID-007** (Plan-ID) Security & Cross-Tenant Tests — **Roadmap-Status: DONE**

Die Repository-ID **ID-007** ist mit dem anderen Inhalt „article permissions“
belegt und DONE. Dieser Repository-Task deckt den Plan-Task ID-007 nicht ab.
Cross-Tenant-Prüfungen existieren verstreut in den Endpunkttests, aber ohne
eigenen Task und ohne eigene Abnahme.

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

## ID-003a – Role-Permission-Zuordnung

Bewusst eingeschobener Task, im ursprünglichen Plan nicht vorgesehen.

Der Rollenkatalog (ID-002) und der Permission-Katalog (ID-003) stehen
unverbunden nebeneinander. ID-003a verbindet sie über `identity.role_permission`
gemäß der in D-20 freigegebenen Matrix.

Die Zuordnung erforderte eine eigene fachliche Freigabe und konnte deshalb
nicht Teil von ID-003 sein: welche Rolle welche Rechte bündelt, ist eine
Sicherheitsentscheidung und keine technische Ableitung.

Nicht enthalten: Authorization Middleware, Endpoint-Policies, Claims. Diese
folgen mit ID-006.

## OPS-001 – Initial Platform Administrator Bootstrap

Zweck: Erstanlage des ersten `PlatformAdmin`. `organization.manage` ist laut
D-20 sowohl `PlatformAdmin` als auch `OrganizationAdmin` zugewiesen. Für das
Anlegen der ersten Organisation hilft jedoch keine der beiden Zuweisungen:
`OrganizationAdmin` erhält die Permission über ein
`OrganizationRoleAssignment` und kann sie deshalb nur innerhalb einer bereits
zugewiesenen Organisation nutzen; bei der ersten Organisation existiert dieser
Kontext noch nicht. Die Zuweisung an `PlatformAdmin` stammt dagegen aus einem
`PlatformRoleAssignment`. Nach D-27 gelten Platform Permissions ausschließlich
im Platform Scope und gewähren keinen Zugriff über organisationsgebundene
Routen. Solange noch kein `PlatformAdmin` existiert, kann daher niemand über
einen Platform-Endpunkt die erste Organisation anlegen.

Verbindliche Anforderungen gemäß D-34:

- Expliziter administrativer CLI-Bootstrap.
- Kein automatischer Seed.
- Keine Erstellung beim normalen Application-Startup.
- Keine Erstellung durch EF-Migrationen.
- Keine Default-Credentials.
- Keine Secrets im Repository.
- Erfordert eine ausdrückliche Operator-Aktion.
- Benutzer, Credential und `PlatformRoleAssignment` entstehen über die
  regulären Application- und Domain-Pfade, nicht per direktem SQL.
- Ausschließlich für die initiale Plattformadministration gedacht.
- Existiert bereits ein `PlatformAdmin`, lehnt der normale Bootstrap
  standardmäßig ab.
- Das normale Login läuft danach unverändert über die vorhandene Auth-API.

## ID-008 – User Management

Zweck: Benutzer über die API anlegen und verwalten. Die Permissions
`user.read` und `user.manage` sind seit ID-003 geseedet und in D-20 Rollen
zugeordnet, werden aber von keinem Endpunkt ausgewertet.

Hinweis zur ID: ID-007 ist im Repository bereits mit einem anderen Inhalt
belegt. ID-008 ist deshalb eine neue ID und keine Umbenennung.

# EPIC 2 – Organizations

- **ORG-001a** Organization- und Location-Persistence Foundation (eingeschoben) — **Roadmap-Status: DONE**
- **ORG-001** Organization CRUD — **Roadmap-Status: DONE** — ein PlatformAdmin kann Organisationen über den Platform-Endpunkt anlegen und einzeln abrufen; Ändern, Löschen und Auflisten sind nicht Teil von ORG-001.
- **ORG-002** Location CRUD — **Roadmap-Status: IN_PROGRESS** — Repository-Task ORG-002 lieferte ausschließlich das Anlegen; ORG-002b ergänzte den Lesezugriff, während Ändern und Löschen weiterhin fehlen, weshalb der Plan-Task IN_PROGRESS bleibt.
- **ORG-002b** Location Read/List — **Roadmap-Status: DONE**
- **ORG-003** Membership Management — **Roadmap-Status: DONE** — PlatformAdmins können Benutzer über die Platform-API als Organisationsmitglieder aufnehmen und ihnen organisationsweite Rollen zuweisen.
- **ORG-004** Tenant Isolation Integration Tests — **Roadmap-Status: NOT_STARTED**

## ORG-001a – Organization- und Location-Persistence Foundation

Bewusst eingeschobener Task, im ursprünglichen Plan nicht vorgesehen. Er wird
**vor** ORG-001 ausgeführt.

Grund: ID-004 (Organization Membership) verweist laut D-22 auf
`org.organization` und `org.location`. Beide Tabellen existierten nicht, und
ohne sie liessen sich weder Fremdschlüssel setzen noch die Regel durchsetzen,
dass ein Standort zur selben Organisation gehören muss. Tabellen mit nackten
UUID-Spalten ohne Fremdschlüssel anzulegen widerspricht `AGENTS.md` §36.

ORG-001 („Organization CRUD") setzt Authentifizierung und Autorisierung
voraus, die es noch nicht gibt. ORG-001a bricht diese Zirkularität auf:
Domain-Modell und Persistenz ohne API, analog zu ID-002a.

Scope: `Organization` und `Location` als Domain-Modell, `OrganizationsDbContext`,
Schema `org` mit eigener Migration-History, erste Migration.

Nicht enthalten: API, CRUD-Endpunkte, Application Services, Mitgliedschaften,
Rollenzuweisungen.

## ORG-003 – Membership Management

Die Platform-API schließt den Setup-Pfad durch das Anlegen einer
Organisationsmitgliedschaft und die Zuweisung organisationsweiter Rollen. Die
Endpunkte verwenden die bestehenden Platform-Permissions `user.manage` und
`user.read`; D-27 bleibt unverändert.

Standortbezogene Rollenzuweisungen sind **DEFERRED**. Kein Pilot-Ablauf braucht
sie bisher. Das Datenmodell und der zusammengesetzte Fremdschlüssel auf
`org.location` bleiben erhalten und werden nicht zurückgebaut; die API exponiert
`locationId` nicht.

Die Selbstverwaltung durch OrganizationAdmins ist **DEFERRED**. Sie braucht
zuerst den in D-37 als offen geführten sicheren Invite- beziehungsweise
Lookup-Mechanismus. Ein späterer Endpunkt dafür liegt getrennt unter
`/api/v1/organizations/{organizationId}/members`.

## ORG-002b – Location Read/List

Zweck: Schließt ausschließlich den für den Pilot benötigten Rest von ORG-002.
Der Repository-Task ORG-002 lieferte nur das Anlegen.

Ausdrücklich nicht im Scope sind Update und Delete. Sie werden nicht
automatisch mitgezogen.

Milestone: `M2 – Organizations Ready`

# EPIC 3 – Catalog

Die CAT-Nummerierung weicht vollständig ab: Dieselbe ID bezeichnet im Plan und
im Repository unterschiedliche Inhalte.

- **CAT-001** (Plan-ID) Product Category — **Roadmap-Status: NOT_STARTED**. Die Repository-ID **CAT-001** bezeichnet stattdessen die Product Foundation für Plan-Task CAT-003. Nach Abschluss der Recovery-Kette wird CAT-001 gegen den Pilotbedarf bewertet. Falls der Task dann nicht erforderlich ist, wird er ausdrücklich auf **DEFERRED** gesetzt und nicht stillschweigend übergangen; diese Bewertung steht noch aus.
- **CAT-002** (Plan-ID) Unit — **Roadmap-Status: DONE** — ausgeliefert als Repository-Task **CAT-003** „unit catalog“.
- **CAT-003** (Plan-ID) Product — **Roadmap-Status: IN_PROGRESS** — ausgeliefert als Repository-Task **CAT-001** „product foundation“; Domainmodell und Persistenz stehen, ein Produkt-Endpunkt fehlt.
- **CAT-004** (Plan-ID) Article/SKU — **Roadmap-Status: DONE** — ausgeliefert als Repository-Tasks **CAT-002a** (article persistence) und **CAT-002b** (article API).
- **CAT-005** minimale Product Profile Foundation — **Roadmap-Status: NOT_STARTED** — Nach Abschluss der Recovery-Kette wird CAT-005 gegen den Pilotbedarf bewertet. Falls der Task dann nicht erforderlich ist, wird er ausdrücklich auf **DEFERRED** gesetzt und nicht stillschweigend übergangen; diese Bewertung steht noch aus.
- **CAT-006** Product API — **Roadmap-Status: DONE**

## CAT-006 – Product API

Zweck: Produkte über die API anlegen und lesen. Modell und Persistenz stehen
seit dem Repository-Task CAT-001; die Permissions `product.read`,
`product.create` und `product.update` sind geseedet, werden aber von keinem
Endpunkt ausgewertet.

Hinweis zur ID: Der Plan-Task CAT-003 „Product“ bleibt **IN_PROGRESS** und wird
nicht umgewidmet.

Milestone: `M3 – Catalog Ready`

# EPIC 4 – Traceability Core

- **TRC-001** Lot Domain Model — **Roadmap-Status: DONE**
- **TRC-002** Lot Persistence, Migration, Constraints, Indizes — **Roadmap-Status: DONE**
- **TRC-003** Create Lot API + Permission + Scope — **Roadmap-Status: DONE**
- **TRC-004** Lot Read/List + Pagination/Filter — **Roadmap-Status: DONE**
- **TRC-005** Event Types — **Roadmap-Status: NOT_STARTED**
- **TRC-006** Traceability Event Domain Model — **Roadmap-Status: NOT_STARTED**
- **TRC-007** Event Persistence — **Roadmap-Status: NOT_STARTED**
- **TRC-008** Create Traceability Event, mehrere Inputs/Outputs — **Roadmap-Status: NOT_STARTED**
- **TRC-009** Cycle Protection — **Roadmap-Status: NOT_STARTED**
- **TRC-010** Backward Trace — **Roadmap-Status: NOT_STARTED**
- **TRC-011** Forward Trace — **Roadmap-Status: NOT_STARTED**
- **TRC-012** Graph Response Model — **Roadmap-Status: NOT_STARTED**
- **TRC-013** End-to-End Traceability Tests — **Roadmap-Status: NOT_STARTED**
- **TRC-014** Mixing Test — **Roadmap-Status: NOT_STARTED**
- **TRC-015** Split Test — **Roadmap-Status: NOT_STARTED**
- **TRC-016** Cross-Tenant Traceability Test — **Roadmap-Status: NOT_STARTED**
- **TRC-017** Performance Baseline — **Roadmap-Status: NOT_STARTED**

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

- **QLT-001** Quality Parameter — **Roadmap-Status: NOT_STARTED**
- **QLT-002** Sample — **Roadmap-Status: NOT_STARTED**
- **QLT-003** Lab Result — **Roadmap-Status: NOT_STARTED**
- **QLT-004** Specification — **Roadmap-Status: NOT_STARTED**
- **QLT-005** Lot Block — **Roadmap-Status: NOT_STARTED**
- **QLT-006** Lot Release — **Roadmap-Status: NOT_STARTED**
- **QLT-007** Blocked Lot Logistics Guard — **Roadmap-Status: NOT_STARTED**
- **QLT-008** Authorization Tests — **Roadmap-Status: NOT_STARTED**

Milestone: `M5 – Quality Ready`

# EPIC 6 – Documents

- **DOC-001** Document Metadata — **Roadmap-Status: NOT_STARTED**
- **DOC-002** Object Storage Abstraction — **Roadmap-Status: NOT_STARTED**
- **DOC-003** Upload API + Validation — **Roadmap-Status: NOT_STARTED**
- **DOC-004** Links zu Lot/Sample/Organization/Delivery — **Roadmap-Status: NOT_STARTED**

Milestone: `M6 – Documents Ready`

# EPIC 7 – Logistics

- **LOG-001** Transport — **Roadmap-Status: NOT_STARTED**
- **LOG-002** Transport Item — **Roadmap-Status: NOT_STARTED**
- **LOG-003** Delivery — **Roadmap-Status: NOT_STARTED**
- **LOG-004** Delivery Item — **Roadmap-Status: NOT_STARTED**
- **LOG-005** Blocked Lot Guard — **Roadmap-Status: NOT_STARTED**
- **LOG-006** Forward Trace zeigt Lieferungen/Empfänger — **Roadmap-Status: NOT_STARTED**

Milestone: `M7 – Logistics Ready`

# EPIC 8 – Public Trace / QR

- **PUB-001** Trace Code / Public Token — **Roadmap-Status: NOT_STARTED**
- **PUB-002** Public Trace Profile — **Roadmap-Status: NOT_STARTED**
- **PUB-003** Public Trace API — **Roadmap-Status: NOT_STARTED**
- **PUB-004** Public Data Security Tests — **Roadmap-Status: NOT_STARTED**
- **PUB-005** QR Generation — **Roadmap-Status: NOT_STARTED**

Milestone: `M8 – Public Trace Ready`

# EPIC 9 – Audit

- **AUD-001** Audit Model — **Roadmap-Status: NOT_STARTED**
- **AUD-002** Audit Coverage für kritische Entities — **Roadmap-Status: NOT_STARTED**
- **AUD-003** Audit Read API — **Roadmap-Status: NOT_STARTED**
- **AUD-004** Audit Integrity Tests — **Roadmap-Status: NOT_STARTED**

Milestone: `M9 – Audit Ready`

# EPIC 10 – Olive Oil Pilot

- **OLV-001** Olive Oil Product Profile — **Roadmap-Status: NOT_STARTED**
- **OLV-002** Harvest Data — **Roadmap-Status: NOT_STARTED**
- **OLV-003** Pressing Parameters — **Roadmap-Status: NOT_STARTED**
- **OLV-004** Oil Yield — **Roadmap-Status: NOT_STARTED**
- **OLV-005** Olive Oil Quality Configuration — **Roadmap-Status: NOT_STARTED**
- **OLV-006** kompletter Pilot-End-to-End-Test — **Roadmap-Status: NOT_STARTED**

Milestone: `M10 – Pilot 1 Backend Complete`

# EPIC 11 – Frontend

- **UI-001** Auth — **Roadmap-Status: NOT_STARTED**
- **UI-002** Dashboard — **Roadmap-Status: NOT_STARTED**
- **UI-003** Organizations/Locations — **Roadmap-Status: NOT_STARTED**
- **UI-004** Users/Roles — **Roadmap-Status: NOT_STARTED**
- **UI-005** Products/Articles — **Roadmap-Status: NOT_STARTED**
- **UI-006** Lots — **Roadmap-Status: NOT_STARTED**
- **UI-007** Lot Detail — **Roadmap-Status: NOT_STARTED**
- **UI-008** Traceability Event Create — **Roadmap-Status: NOT_STARTED**
- **UI-009** Traceability Graph — **Roadmap-Status: NOT_STARTED**
- **UI-010** Quality — **Roadmap-Status: NOT_STARTED**
- **UI-011** Documents — **Roadmap-Status: NOT_STARTED**
- **UI-012** Logistics — **Roadmap-Status: NOT_STARTED**
- **UI-013** QR Management — **Roadmap-Status: NOT_STARTED**
- **UI-014** Public Consumer Page — **Roadmap-Status: NOT_STARTED**
- **UI-015** Audit Viewer — **Roadmap-Status: NOT_STARTED**

Milestone: `M11 – Pilot UI Complete`

# EPIC 12 – Hardening

- **E2E-001** Full Pilot Scenario — **Roadmap-Status: NOT_STARTED**
- **E2E-002** Authorization Matrix — **Roadmap-Status: NOT_STARTED**
- **E2E-003** Tenant Isolation für alle Kernendpunkte — **Roadmap-Status: NOT_STARTED**
- **E2E-004** Traceability Graph Load Test — **Roadmap-Status: NOT_STARTED**
- **E2E-005** Backup/Restore Test — **Roadmap-Status: NOT_STARTED**
- **E2E-006** Security Review — **Roadmap-Status: NOT_STARTED**
- **E2E-007** Release Checklist — **Roadmap-Status: NOT_STARTED**

Milestone: `M12 – Pilot 1 Release Candidate`

## Milestone-Status

- **M0 – Foundation Ready** — **ERREICHT**.
- **M1 – Identity Ready** — **ERREICHT**.
- **M2 – Organizations Ready** — **NICHT ERREICHT**. Offen: **ORG-002** und **ORG-004**.
- **M3 – Catalog Ready** — **NICHT ERREICHT**. Offen: **CAT-001**, **CAT-003** und **CAT-005**.
- **M4 – Traceability Core Proven** — **NICHT ERREICHT**. Offen: **TRC-005**, **TRC-006**, **TRC-007**, **TRC-008**, **TRC-009**, **TRC-010**, **TRC-011**, **TRC-012**, **TRC-013**, **TRC-014**, **TRC-015**, **TRC-016** und **TRC-017**.
- **M5 – Quality Ready** — **NICHT ERREICHT**. Offen: **QLT-001**, **QLT-002**, **QLT-003**, **QLT-004**, **QLT-005**, **QLT-006**, **QLT-007** und **QLT-008**.
- **M6 – Documents Ready** — **NICHT ERREICHT**. Offen: **DOC-001**, **DOC-002**, **DOC-003** und **DOC-004**.
- **M7 – Logistics Ready** — **NICHT ERREICHT**. Offen: **LOG-001**, **LOG-002**, **LOG-003**, **LOG-004**, **LOG-005** und **LOG-006**.
- **M8 – Public Trace Ready** — **NICHT ERREICHT**. Offen: **PUB-001**, **PUB-002**, **PUB-003**, **PUB-004** und **PUB-005**.
- **M9 – Audit Ready** — **NICHT ERREICHT**. Offen: **AUD-001**, **AUD-002**, **AUD-003** und **AUD-004**.
- **M10 – Pilot 1 Backend Complete** — **NICHT ERREICHT**. Offen: **OLV-001**, **OLV-002**, **OLV-003**, **OLV-004**, **OLV-005** und **OLV-006**.
- **M11 – Pilot UI Complete** — **NICHT ERREICHT**. Offen: **UI-001**, **UI-002**, **UI-003**, **UI-004**, **UI-005**, **UI-006**, **UI-007**, **UI-008**, **UI-009**, **UI-010**, **UI-011**, **UI-012**, **UI-013**, **UI-014** und **UI-015**.
- **M12 – Pilot 1 Release Candidate** — **NICHT ERREICHT**. Offen: **E2E-001**, **E2E-002**, **E2E-003**, **E2E-004**, **E2E-005**, **E2E-006** und **E2E-007**.

Die Arbeit an EPIC 4 wurde begonnen, obwohl M1, M2 und M3 offen sind. Das war
keine Entscheidung, sondern ist unbemerkt entstanden, weil der Plan bis dahin
keinen Status je Task führte.

## Recovery-Reihenfolge

1. **OPS-001** Initial Platform Administrator Bootstrap — **ERLEDIGT**
2. **ORG-001** Organization Create — **ERLEDIGT**
3. **ID-008** User Management — **ERLEDIGT**
4. **ORG-003** Membership + Organization Role Assignment — **ERLEDIGT**
5. **CAT-006** Product API — **erledigt**
6. **ORG-002b** Location Read/List — **ERLEDIGT**
7. **ID-007** Security & Cross-Tenant Abnahme — **ERLEDIGT**
8. **M1, M2 und M3** erneut prüfen
9. Erst danach **TRC-005** und folgende

Diese Reihenfolge folgt dem Setup-Pfad und nicht der Tasknummerierung. Der
Zielpfad lautet: Organisation → Standort → Benutzer → Membership und Rolle →
Produkt/Artikel → Lot → Traceability Event → Transformation → vollständige
Vorwärts- und Rückwärtsverfolgung.

Der Pilot darf für diesen Ablauf dauerhaft weder auf Test-Fixtures noch auf
manuelle SQL-Eingriffe angewiesen sein.

# Spätere Epics

- **EPIC 13 Dairy:** Herd, Milking, Tanks, Cooling, Mixing, Pasteurization, Recipes, Maturation
- **EPIC 14 Meat:** Animal Identity, Slaughter, Carcass, Cutting, Packaging, Cold Chain
- **EPIC 15 Inspection Portal:** Inspections, Inspector, Checklists, Findings, Corrective Actions, Signatures, Reports

Diese Epics starten erst, wenn Pilot 1 stabil ist.

# Backlog

Kleine Findings aus Reviews, die keinem Epic zugeordnet sind und einzeln
umgesetzt werden.

## FIX-006 – `errorCode` in der automatischen Validierungsantwort

**Status:** OFFEN
**Herkunft:** Review zu FIX-004

Jede Fehlerantwort der API trägt einen `errorCode` — `AUTHENTICATION_FAILED`,
`AUTHORIZATION_DENIED`, `ARTICLE_CONFLICT`, `RATE_LIMIT_EXCEEDED`,
`UNHANDLED_ERROR` und weitere. **Ausgenommen ist die automatische
400-Antwort der Modellvalidierung aus `[ApiController]`**: sie enthält
`correlationId` und `traceId`, aber keinen `errorCode`.

Ein Client, der `errorCode` einheitlich auswertet, findet ihn dort nicht.

**Ziel:** Ein eigener Code, etwa `VALIDATION_FAILED`, in der automatischen
`ValidationProblemDetails`-Antwort. Die zentrale `ApiProblemDetailsFactory` aus
FIX-004 ist die Stelle dafür; die Feldstruktur der Antwort bleibt sonst
unverändert.

**Warum nicht nebenbei erledigt:** Das ändert das Laufzeitverhalten der API und
den Fehlervertrag gegenüber Clients. Das ist keine Dokumentationskorrektur und
gehört in einen eigenen Task mit eigenen Tests.

## FIX-007 – Toter `decimal.MinValue`-Sonderfall in `CreateLotService`

**Status:** OFFEN
**Herkunft:** Review zu TRC-003, CR-01 [MINOR]

Die Mengenprüfung in `CreateLotService` enthält den Sonderfall
`command.Quantity == decimal.MinValue ||` vor der Bereichsprüfung. Er ist
unerreichbar: `Math.Abs(decimal.MinValue)` wirft nicht, sondern liefert
`79228162514264337593543950335`, weil `decimal` im Gegensatz zu `int`
symmetrisch ist — Vorzeichen und Betrag sind getrennt gespeichert. Im Review
nachgemessen.

**Ziel:** Den Sonderfall entfernen, sodass nur noch
`Math.Abs(quantity) > MaximumSupportedQuantity` steht. Verhalten und Tests
bleiben unverändert.

**Warum nicht nebenbei erledigt:** Der Code ist funktional korrekt, nur
irreführend — er suggeriert eine Überlaufgefahr, die es nicht gibt. Eine
Änderung an der Mengenvalidierung ohne eigenen Task und eigene Testabnahme wäre
unverhältnismäßig.

## FIX-008 – `UnitCode.Create` läuft dreifach pro Lot-Anlage

**Status:** OFFEN
**Herkunft:** Review zu TRC-003, CR-02 [MINOR]

Beim Anlegen eines Lots wird `UnitCode.Create` dreimal ausgeführt: in
`UnitQueryService.FindIdByCodeAsync`, erneut in
`UnitReader.FindIdByCodeAsync` und ein drittes Mal im `LotsController`, um für
die Antwort `normalizedUnitCode` zu bilden. Der dritte Aufruf verlässt sich
stillschweigend darauf, dass eine nicht-null Unit-Id einen gültigen Code
impliziert. Das stimmt, ist aber nirgends festgehalten und bricht still, sobald
jemand die Reihenfolge ändert.

**Ziel:** `FindIdByCodeAsync` liefert den normalisierten Code gemeinsam mit der
Id zurück, sodass Controller und Reader ihn nicht erneut herleiten müssen. Die
Normalisierung bleibt einmalig im `UnitQueryService`.

**Warum nicht nebenbei erledigt:** Das ändert eine öffentliche
Application-Signatur im Catalog-Modul und betrifft damit auch künftige
Aufrufer. Gehört in einen eigenen Task.

## FIX-009 - `LoginRequest` gibt das Passwort in `ToString()` preis

**Status:** OFFEN
**Herkunft:** Review zu OPS-001, Nachbarbefund

`LoginRequest` in
`src/Modules/Identity/FoodTraceability.Modules.Identity.Application/Authentication/AuthenticationModels.cs`
ist ein positional Record mit dem Member `string? Password` und überschreibt
`ToString()` nicht. C# erzeugt für Records automatisch eine `ToString()`-
Implementierung, die alle Member ausgibt. Würde ein Log- oder Diagnoseaufruf
dieses Objekt formatieren, gäbe er damit das Klartextpasswort aus.

Heute wird das Objekt nirgends formatiert. Es handelt sich daher nicht um eine
aktive Preisgabe, sondern um eine offene Flanke. Der Befund besteht seit
ID-005b.

**Ziel:** `ToString()` so überschreiben, dass ein fester Platzhalter das
Passwort ersetzt und auch dessen Länge nicht erscheint. Ein Unit-Test weist
dieses Verhalten nach. Als Vorlage dient die in OPS-001 korrigierte
`BootstrapPlatformAdministratorCommand`.

**Warum nicht nebenbei erledigt:** Der Befund liegt außerhalb des OPS-001-Scope
und berührt den Login-Pfad. Eine Änderung dort gehört in einen eigenen Task mit
eigener Abnahme.

## FIX-010 - Zentrale Normalisierung persistierter Zeitpunkte

**Status:** ERLEDIGT
**Herkunft:** Review zu ORG-001, D-36

**Umsetzung:** Die zentrale Zeitstempelnormalisierung wurde in FIX-010 umgesetzt.

**Inhalt:** Umsetzung von D-36 über alle betroffenen Module. Eine zentrale
Zeitquelle liefert Werte bereits in Mikrosekundenauflösung, sodass Entität,
Datenbank und API-Antwort denselben Wert tragen.

**Acceptance Criterion:** Create-Response und ein anschließender GET liefern
für persistierte Zeitstempel **exakt** denselben Wert, nicht nur innerhalb
einer Toleranz. Zu prüfen für Articles, Lots, Locations und Organizations.

Die Regressionstests müssen so gewählt sein, dass sie tatsächlich fehlschlagen,
wenn die zentrale Normalisierung entfernt wird. Ein Test, der auch ohne die
Normalisierung grün bleibt, erfüllt das Kriterium nicht.

**Warum nicht in ORG-001 erledigt:** Die Änderung betrifft vier Module und die
gemeinsame Zeitquelle. Das liegt außerhalb des ORG-001-Scope und braucht eine
eigene Abnahme.

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
