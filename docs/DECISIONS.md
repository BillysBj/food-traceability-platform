# DECISIONS.md
# Food Traceability Platform – Decision Log

Dieses Dokument ist ab DOCS-002 die **Source of Truth für explizite
Architektur- und Modellentscheidungen**.

Bei Widersprüchen zwischen diesem Dokument und `AGENTS.md`,
`ARCHITECTURE.md`, `DEVELOPMENT_PLAN.md`, `docs/MASTER_SPECIFICATION.docx`
oder `docs/MASTER_ER_DIAGRAM.drawio` gilt **dieses Dokument**, sofern die
Entscheidung hier als `ENTSCHIEDEN` geführt wird.

## Regeln für dieses Log

- Jede Entscheidung erhält eine **fortlaufende, nie wiederverwendete** ID
  (`D-01`, `D-02`, …). Eine einmal vergebene Nummer wird niemals einem
  anderen Sachverhalt zugewiesen, auch nicht nach Verwerfen der Entscheidung.
- Status ist entweder `ENTSCHIEDEN` oder `OFFEN`.
- `OFFEN` bedeutet: die Entscheidung ist noch nicht getroffen. Kein Agent darf
  sie durch Implementierung vorwegnehmen. Betroffene Tasks nennen sie
  ausdrücklich als blockierend.
- Eine Entscheidung wird nicht gelöscht. Wird sie revidiert, bleibt der
  Eintrag bestehen und verweist auf die ablösende Entscheidung.
- Neue Entscheidungen werden **am Ende** angehängt. Die nächste freie Nummer
  steht unten unter „Nächste freie ID".

## Übersicht

| ID | Titel | Status |
|----|-------|--------|
| D-01 | Lot-Eigentum bei Organisationswechsel | ENTSCHIEDEN |
| D-02 | Sichtbarkeitsregel im Cross-Organisation-Trace | ENTSCHIEDEN |
| D-03 | `traceable_object`-Supertyp in Pilot 1 | ENTSCHIEDEN |
| D-04 | `organization_id` als durchgängige Tenant-Spalte | ENTSCHIEDEN |
| D-05 | Modellierung plattformweiter Rechte | ENTSCHIEDEN |
| D-06 | API-Pfadpräfix und Versionierung | ENTSCHIEDEN |
| D-07 | Verbindlichkeit und Umfang von i18n | OFFEN |
| D-08 | Semantik von `trace.lot.quantity` | OFFEN |
| D-09 | Einheitenkonvertierung (BR-003) | OFFEN |
| D-10 | Zukunft von `trace.object_relation` | OFFEN |
| D-11 | Regel für Cross-Schema-Fremdschlüssel | ENTSCHIEDEN |
| D-12 | Ablageort des Frontends | OFFEN |
| D-13 | Authentifizierungs- und Token-Modell | ENTSCHIEDEN |
| D-14 | Zielplattform und Versionspinning | ENTSCHIEDEN |
| D-15 | Public Trace Identifier Naming | OFFEN |
| D-16 | Custom Organization Roles | ENTSCHIEDEN |
| D-17 | Default Role Seeding und `role.code` | ENTSCHIEDEN |
| D-18 | Kanonische Permission-Liste Pilot 1 v1 | ENTSCHIEDEN |
| D-19 | Collation-Strategie | ENTSCHIEDEN |
| D-20 | Role-Permission-Matrix Pilot 1 v1 | ENTSCHIEDEN |
| D-21 | Zuweisbarkeit von Rollen (assignment_scope) | ENTSCHIEDEN |
| D-22 | Membership- und Rollenzuweisungsmodell | ENTSCHIEDEN |
| D-23 | Umfang der ASP.NET Core Identity-Nutzung | ENTSCHIEDEN |
| D-24 | Token- und Login-Parameter | ENTSCHIEDEN |
| D-25 | Startverhalten bei fehlender Datenbankkonfiguration | OFFEN |
| D-26 | Organization Context in API Routes | ENTSCHIEDEN |
| D-27 | Platform Permissions ohne Zugriff auf Organisationsressourcen | ENTSCHIEDEN |
| D-28 | Kein physischer `traceable_object`-Supertyp in Pilot 1 | ENTSCHIEDEN |
| D-29 | Lot-Eigentum beim Organisationsuebergang | ENTSCHIEDEN |
| D-30 | Cross-Organization Visibility: Partner View und Public View | ENTSCHIEDEN |

---

## D-01 – Lot-Eigentum bei Organisationswechsel

**Status:** ENTSCHIEDEN (2026-09-02)
**Beantwortet durch:** D-29 – Variante (a) wurde gewählt. Der folgende Text bleibt
als Herleitung der Frage stehen.
**Betraf:** TRC-003, TRC-008, LOG-003, ORG-004

`trace.lot.organization_id` zusammen mit `UNIQUE (organization_id, lot_number)`
impliziert Eigentum. Die Pilot-Kette Producer → Mill → Bottler → Retailer
überschreitet zwingend Organisationsgrenzen. Kein Dokument legt fest, ob beim
`RECEIVE` ein neues Lot der empfangenden Organisation entsteht oder ob
`organization_id` wechselt.

**Optionen:** (a) neues Lot pro Organisation, verknüpft über ein
`RECEIVE`-Event; (b) `organization_id` wandert mit dem Lot; (c) Lot bleibt beim
Erzeuger, Empfänger erhält einen Lese-Grant.

**Empfehlung bis zur Klärung:** (a) – nutzt ausschließlich den bereits
definierten Event-Mechanismus und ist mit D-04 konfliktfrei.

---

## D-02 – Sichtbarkeitsregel im Cross-Organisation-Trace

**Status:** ENTSCHIEDEN (2026-09-02)
**Beantwortet durch:** D-30. Der folgende Text bleibt als Herleitung der Frage stehen.
**Betraf:** TRC-010, TRC-011, TRC-016, PUB-003, PUB-004

Master Specification 5.4 verweist auf eine „relationship/trace disclosure
rule", die in keinem Dokument existiert. Backward Trace liefert per Definition
Lots fremder Organisationen; welche Felder ein nachgelagerter Partner sehen
darf, ist ungeklärt.

**Empfehlung bis zur Klärung:** Traversierung endet an der
Organisationsgrenze, fremde Knoten erscheinen anonymisiert. Die
Sichtbarkeitsentscheidung liegt an genau einer Stelle im Algorithmus, damit
eine spätere Lockerung additiv möglich ist.

---

## D-03 – `traceable_object`-Supertyp in Pilot 1

**Status:** ENTSCHIEDEN (2026-09-02)
**Beantwortet durch:** D-28 – Variante (b) wurde gewählt. Der folgende Text bleibt
als Herleitung der Frage stehen.
**Betraf:** TRC-002, TRC-007, LOG-002, LOG-004, PUB-001

Das ER-Diagramm führt `trace.traceable_object` als Supertyp; alle Referenzen
laufen über `traceable_object_id`. Master Specification 6.2 und `AGENTS.md`
§10/§20 verwenden durchgängig `lot_id`. Der Widerspruch betrifft praktisch
jeden Fremdschlüssel im Kernmodell.

**Empfehlung bis zur Klärung:** nur `trace.lot` physisch, aber Signaturen und
DTOs sprechen von `traceableObjectId`, damit ein Supertyp später additiv
einziehen kann (entspricht Master Specification 4.1).

---

## D-04 – `organization_id` als durchgängige Tenant-Spalte

**Status:** ENTSCHIEDEN (2026-08-31)

`organization_id` ist die durchgängige Mandantenspalte auf **allen**
mandantenbezogenen fachlichen Tabellen. Global gültige Referenz- und
Konfigurationstabellen brauchen sie nicht. Beziehungen zwischen
mandantenbezogenen Entitäten dürfen keine organisationsübergreifenden
Verknüpfungen ermöglichen.

**Begründung:** Ohne redundante Mandantenspalte ist Isolation nur über
Join-Ketten erzwingbar; ein einziger vergessener Join wäre ein
Cross-Tenant-Leak.

**Ausnahme:** `identity.user` ist eine globale Identitätsentität. Ein Benutzer
kann mehreren Organisationen angehören; der Organisationsbezug liegt an der
Rollenzuweisung, nicht am Benutzer. Siehe ID-001.

---

## D-05 – Modellierung plattformweiter Rechte

**Status:** ENTSCHIEDEN (2026-09-01)

Plattformweite Rollen und Rechte werden **explizit getrennt** von
organisationsgebundenen Rollenzuweisungen modelliert.
`organization_id = NULL` darf **niemals** implizit Plattformzugriff bedeuten.

**Begründung:** Nullable-Spalten in Sicherheitsprädikaten sind eine häufige
Quelle stiller Autorisierungsfehler. Plattformrechte sollen explizit und
auditierbar sein.

**Hinweis:** Eine globale Rollen*definition* bedeutet keinen globalen
*Zugriff*. Der Katalog aus D-16 ist global; die Trennung findet in der
Zuweisung statt.

---

## D-06 – API-Pfadpräfix und Versionierung

**Status:** ENTSCHIEDEN (2026-09-01), umgesetzt in DOCS-001

- `/api/v1` für die authentifizierte API
- `/api/public/v1` für öffentliche Endpunkte
- `/swagger/v1/swagger.json` bleibt unverändert

Beispiele: `POST /api/v1/auth/login`, `GET /api/v1/lots/{id}`,
`GET /api/public/v1/trace/{code}`.

**Präzisiert durch:** D-26 – Organization Context in API Routes. Das obige
Beispiel `GET /api/v1/lots/{id}` ist für eine tenantgebundene Ressource
überholt; maßgeblich ist der Pfad mit Organisationskontext. D-06 regelt
ausschließlich Präfix und Versionierung.

**Begründung:** Master Specification 11.1 ist an dieser Stelle präziser als
`AGENTS.md` §30, passt zum ohnehin vorgeschriebenen Swagger-Pfad, und ein
gedruckter QR-Pfad ohne Version wäre nicht mehr änderbar.

---

## D-07 – Verbindlichkeit und Umfang von i18n

**Status:** OFFEN

Die Master Specification enthält **keine** i18n-Anforderung; das ER-Diagramm
hat keine Translation-Tabellen. `AGENTS.md`, `ARCHITECTURE.md`,
`DEVELOPMENT_PLAN.md` und `CLAUDE_REVIEWER.md` erklären Multilanguage dagegen
für verbindlich.

**Zu entscheiden:** Gilt der i18n-Block als Ergänzung der Spezifikation?
Welche Entitäten erhalten `*_translation`-Tabellen? Braucht `identity.user`
eine bevorzugte Sprache? Wie wählt Public Trace die Sprache?

**Auswirkung bisher:** Seed-Daten für Rollen (D-17) und Permissions (D-18)
lassen `description` bewusst leer, um keine englische Anzeigeprosa
festzuschreiben, die später übersetzt werden müsste.

---

## D-08 – Semantik von `trace.lot.quantity`

**Status:** OFFEN
**Blockiert:** TRC-002, TRC-008

Ist `quantity` die Initialmenge oder der verfügbare Restbestand? Es gibt kein
Bilanz- oder Verbrauchsmodell, obwohl BR-011 („mass/volume balance rules")
eines voraussetzt.

**Empfehlung bis zur Klärung:** Initialmenge, unveränderlich; Bestand aus
Events ableitbar. Passt zu BR-008 („append-oriented").

---

## D-09 – Einheitenkonvertierung (BR-003)

**Status:** OFFEN

BR-003 verlangt kompatible Einheitendimensionen. `catalog.unit` hat
`dimension` und laut Spec 6.2 „conversion metadata where safe", aber weder ER
noch Spec definieren eine Konvertierungsstruktur.

**Empfehlung bis zur Klärung:** Pilot 1 erzwingt Dimensions- **und**
Einheitengleichheit. Die strengere Regel lässt sich später lockern, ohne
bereits erfasste Daten falsch werden zu lassen.

---

## D-10 – Zukunft von `trace.object_relation`

**Status:** OFFEN

`trace.object_relation` (parent/child/relation_type) existiert ausschließlich
im ER-Diagramm. Die Abstammung ist bereits vollständig über
`event_input`/`event_output` definiert. Zwei parallele Lineage-Quellen sind
ein Datenintegritätsrisiko.

**Empfehlung bis zur Klärung:** für Pilot 1 streichen. Falls behalten, dann
eng begrenzt auf Behälterschachtelung und niemals für Abstammung.

---

## D-11 – Regel für Cross-Schema-Fremdschlüssel

**Status:** ENTSCHIEDEN (2026-09-01)

Schemaübergreifende Fremdschlüssel sind im modularen Monolithen **erlaubt**,
wenn sie ausschließlich auf einen Primärschlüssel oder einen eindeutigen
Constraint einer fremden Tabelle zeigen.

**Direkte Schreibzugriffe eines Moduls auf Tabellen eines anderen Moduls
bleiben strikt verboten.** Fachliche Änderungen an einem fremden Modul dürfen
ausschließlich über dessen Application- oder API-Abstraktionen erfolgen.

Ein Cross-Schema-Fremdschlüssel dient **nur der referenziellen Integrität** und
begründet **keine Ownership** der referenzierten Daten.

Ergänzende Regeln:

- Ziel eines Cross-Schema-Fremdschlüssels muss PK oder UNIQUE sein.
- `ON DELETE CASCADE` über Modulgrenzen ist standardmäßig nicht zulässig.
  Bevorzugt `RESTRICT` beziehungsweise `NO ACTION`, sofern nicht ausdrücklich
  anders entschieden.
- Keine modulübergreifende EF-Navigation darf zum Ändern fremder Aggregate
  verwendet werden.
- Lesen fremder Daten erfolgt über Application Services, Read Models oder
  ausdrücklich vorgesehene Read Queries.

**Begründung:** Das Datenmodell verlangt solche Verweise an vielen Stellen —
`quality.sample → trace.lot`, `logistics.delivery_item → trace.lot`, sämtliche
Industry-Detailtabellen auf `trace.traceability_event`. Ein generelles Verbot
wäre nicht durchhaltbar. Was die Modulgrenze schützt, ist das Schreibverbot,
nicht das Leseverbot.

**Praktische Folge:** Ein Fremdschlüssel auf eine Tabelle eines anderen Moduls
kann nicht über die EF-Modellkonfiguration entstehen, weil das den Typ des
fremden Moduls erfordern würde und die Modulisolation bräche. Er wird
stattdessen in der Migration explizit über Tabellen- und Schemanamen angelegt
und durch einen Integrationstest abgesichert.

---

## D-12 – Ablageort des Frontends

**Status:** OFFEN

Weder `src/`, `frontend/` noch `apps/web/` ist festgelegt. `I18N-001` ist im
`DEVELOPMENT_PLAN.md` definiert, aber keinem Epic und keinem Meilenstein
zugeordnet.

**Bisherige Handhabung:** FND-001 bis FND-006 haben bewusst **keinen**
Frontend-Ordner angelegt.

**Empfehlung bis zur Klärung:** `frontend/` auf Repository-Ebene, damit die
Node-Toolchain nicht innerhalb von `src/` liegt.

---

## D-13 – Authentifizierungs- und Token-Modell

**Status:** ENTSCHIEDEN (2026-09-01)
**Betrifft:** ID-005, ID-006

- ASP.NET Core Identity
- kurzlebiges JWT als Access Token
- Refresh Tokens mit Rotation und Widerruf
- Refresh Tokens werden **nicht im Klartext** persistiert
- Externe OIDC-Provider müssen später integrierbar bleiben

**Auswirkung:** ID-001 nimmt bewusst **kein** `password_hash` in das
User-Domain-Modell auf. Ein Benutzer hat unter Umständen gar kein lokales
Passwort; Credentials gehören zur Authentifizierung, nicht zur
Kernidentität.

**Präzisiert durch:** D-23 – Umfang der ASP.NET Core Identity-Nutzung.

---

## D-14 – Zielplattform und Versionspinning

**Status:** ENTSCHIEDEN (2026-08-31)

- .NET 10 (LTS)
- xUnit als Testframework
- Central Package Management über `Directory.Packages.props`
- SDK-Pinning über `global.json`

---

## D-15 – Public Trace Identifier Naming

**Status:** OFFEN
**Zu klären vor:** PUB-001, PUB-003

Der Pfadparameter des öffentlichen Trace-Endpunkts trägt drei verschiedene
Namen:

- `AGENTS.md` §23: `{publicToken}`
- `AGENTS.md` §30: `{code}`
- `ARCHITECTURE.md` §15: `{token}`

DOCS-001 hat ausschließlich die Pfadpräfixe vereinheitlicht (D-06), die
Parameterbenennung bewusst **nicht**.

**Historie:** Diese Entscheidung wurde zunächst als „D-07" bezeichnet; D-07
war bereits für i18n vergeben. Verbindlich ist **D-15**.

---

## D-16 – Custom Organization Roles

**Status:** ENTSCHIEDEN (2026-09-01), umgesetzt in ID-002

Für Pilot 1 dürfen Organisationen **keine eigenen Rollen** definieren.
`identity.role` ist ein global definierter Rollenkatalog. Organisationsspezifische
Rollenverwaltung ist ausdrücklich out of scope. Autorisierung erfolgt
langfristig über Permissions; Rollen sind Permission-Bündel.

**Folge:** `identity.role` trägt keine `organization_id` und keinerlei
Organisationsbezug.

**Historie:** Zunächst als „D-08" bezeichnet; D-08 war bereits für die
Mengensemantik vergeben. Verbindlich ist **D-16**.

---

## D-17 – Default Role Seeding und `role.code`

**Status:** ENTSCHIEDEN (2026-09-01), umgesetzt in ID-002

Die zehn Standardrollen aus Master Specification 5.2 werden deterministisch
als System-/Seed-Daten angelegt: PlatformAdmin, OrganizationAdmin, Producer,
Processor, QualityManager, Laboratory, Bottler, Logistics, Retailer, Auditor.

`identity.role` erhält zusätzlich einen **stabilen, sprachneutralen `code`**
in `SCREAMING_SNAKE_CASE` (`PLATFORM_ADMIN`). Der Code ist der Schlüssel, an
dem Autorisierung hängt. **Anzeigenamen dürfen niemals für
Autorisierungslogik verwendet werden.**

Deterministisch heißt: feste, im Quellcode hinterlegte Guid-Literale,
abgeleitet als `uuid5(DNS, "food-traceability.identity.role.<CODE>")` und
damit unabhängig nachrechenbar. Kein `Guid.NewGuid`.

**Abweichung von der Spezifikation:** Master Specification 6.2 und das
ER-Diagramm kennen kein `role.code`. Das Feld ist eine bewusste Ergänzung
dieser Entscheidung. Die Quelldokumente wurden nicht geändert.

**Historie:** Zunächst als „D-09" bezeichnet; D-09 war bereits für die
Einheitenkonvertierung vergeben. Verbindlich ist **D-17**.

---

## D-18 – Kanonische Permission-Liste Pilot 1 v1

**Status:** ENTSCHIEDEN (2026-09-01), umgesetzt in ID-003

Genau diese 26 Permission-Codes gelten für Pilot 1, Version 1:

```text
organization.read        organization.manage
user.read                user.manage
role.read                permission.read
product.read             product.create           product.update
lot.read                 lot.create               lot.update
trace.read               trace.event.create
quality.read             quality.sample.create    quality.result.create
quality.release          quality.block
document.read            document.upload
transport.read           transport.create
delivery.read            delivery.create
audit.read
```

Permission-Codes sind kleingeschrieben und punktgetrennt, mit mindestens zwei
Segmenten. Sie sind stabile, sprachneutrale technische Identifikatoren;
Autorisierung hängt ausschließlich am Code, niemals an `description` oder
Anzeigenamen.

Seed-Ids sind feste Literale, abgeleitet als
`uuid5(DNS, "food-traceability.identity.permission.<code>")`.

**`shipment.read` und `shipment.create` entfallen.** Logistics verwendet die
fachlich getrennten Begriffe `transport.*` und `delivery.*`.
`ARCHITECTURE.md` §8 führte `shipment.*` und wurde in DOCS-002 angeglichen.

**`identity.role_permission` gehört nicht zu D-18.** Die Zuordnung von
Permissions zu Rollen erfordert eine eigene fachliche Freigabe der
Role-Permission-Matrix.

---

## D-19 – Collation-Strategie

**Status:** ENTSCHIEDEN (2026-09-01), umgesetzt in FND-003

- Es wird **kein** sprachspezifischer Collation-Standard für die Plattform
  festgelegt.
- UTF-8 ist Pflicht.
- Die Datenbank behält ihre Standard-Collation (`en_US.utf8`).
- Sprachspezifische Sortierung erfolgt über **explizite ICU-Collations** dort,
  wo sie gebraucht wird. Die Collations `en` (`en-US`) und `el` (`el-GR`)
  existieren als deterministische ICU-Objekte in der Datenbank.
- Weitere Sprachen sind je eine zusätzliche Migrationszeile.

**Wichtig:** `datcollate` wird bei `CREATE DATABASE` festgelegt und ist danach
nicht mehr änderbar. Für produktive Umgebungen muss die Entscheidung vor dem
Anlegen der Datenbank fallen.

**Historie:** Diese Entscheidung wurde ursprünglich ohne Nummer geführt und
in DOCS-002 als D-19 konsolidiert.

---

## D-20 – Role-Permission-Matrix Pilot 1 v1

**Status:** ENTSCHIEDEN (2026-09-01)
**Setzt voraus:** D-16 (globaler Rollenkatalog), D-17 (Rollen), D-18 (Permissions)

Jede Zuordnung ist aus den Rollenbeschreibungen in Master Specification 5.2
abgeleitet. Grundprinzip ist Least Privilege: ein Recht wird nur vergeben, wenn
die Rolle es für ihre Aufgabe tatsächlich benötigt.

**Grundsatz:** Rollen-Permissions definieren ausschließlich **Fähigkeiten**.
Tenant-/Organization-Scope, Entity-Zugriff und fachliche Zustandsregeln müssen
zusätzlich durchgesetzt werden. **Eine Permission allein darf niemals
organisationsübergreifenden Zugriff ermöglichen.**

Privilegiert und daher besonders zu prüfen: `organization.manage`,
`user.manage`, `lot.update`, `quality.result.create`, `quality.release`,
`quality.block`, `audit.read`.

### PlatformAdmin (9)
```text
organization.read  organization.manage
user.read          user.manage
role.read          permission.read
product.read       product.create      product.update
```
Plattformadministration und Pflege des globalen Produktkatalogs.
**Bewusst ohne Zugriff auf fachliche Kundendaten** (OPEN-A): kein `lot.read`,
`trace.read`, `quality.read` oder `audit.read` aufgrund der Plattformrolle.
Ein späterer Supportzugriff muss separat und auditierbar modelliert werden.

### OrganizationAdmin (5)
```text
organization.read  organization.manage
user.read          user.manage
role.read
```
Benutzer, Standorte und Einstellungen der **eigenen** Organisation.
Kein Schreibzugriff auf globale Produktstammdaten (OPEN-B).
Kein `audit.read` in Pilot 1 (OPEN-F).

### Producer (8), Processor (8), Bottler (8) – identisch
```text
product.read
lot.read           lot.create          lot.update
trace.read         trace.event.create
document.read      document.upload
```
Erzeugung und Transformation von Lots samt zugehöriger Events und Dokumente.

### QualityManager (8)
```text
lot.read           trace.read
quality.read       quality.sample.create
quality.release    quality.block
document.read      document.upload
```
**Bewusst ohne `quality.result.create`:** Laborwerte einzutragen ist Aufgabe
des Laboratory. Wer freigibt, erzeugt nicht die Messwerte.

### Laboratory (5)
```text
lot.read
quality.read       quality.result.create
document.read      document.upload
```
Nur Ergebniserfassung. Ohne `quality.release` und `quality.block`.

### Logistics (8)
```text
lot.read           trace.read
transport.read     transport.create
delivery.read      delivery.create
document.read      document.upload
```

### Retailer (4)
```text
lot.read           trace.read          delivery.read      document.read
```

### Auditor (5)
```text
lot.read           trace.read          quality.read
document.read      audit.read
```
Ausschließlich lesend.

### Auflösung der offenen Punkte

- **OPEN-A** PlatformAdmin erhält standardmäßig keinen Zugriff auf fachliche
  Kundendaten.
- **OPEN-B** Der Pilot-1-Produktkatalog ist global. PlatformAdmin erhält
  `product.create` und `product.update`; OrganizationAdmin keinen Schreibzugriff.
  Organisationsspezifische Artikel/SKUs werden später separat behandelt.
- **OPEN-C** Producer, Processor und Bottler erhalten `lot.update`. Diese
  Permission ist später **zwingend durch Domain-Regeln einzuschränken**: nur
  erlaubte Felder beziehungsweise bearbeitbare Zustände, keine Umschreibung
  bereits fachlich verwendeter oder finalisierter Historie. Danach gelten
  append-orientierte Korrekturen (BR-008).
- **OPEN-D** `document.upload` erhalten Producer, Processor, Bottler,
  QualityManager, Laboratory, Logistics.
- **OPEN-E** `document.read` erhalten zusätzlich Retailer und Auditor.
- **OPEN-F** OrganizationAdmin erhält in Pilot 1 kein `audit.read`.

**Summe:** 68 Zuordnungen. Alle 26 Permissions aus D-18 sind mindestens einer
Rolle zugeordnet.

**Ableitung:** PlatformAdmin erhält `product.read` als notwendige Ergänzung zu
`product.create`/`product.update` — Katalogpflege ohne Lesezugriff wäre nicht
durchführbar. Diese Ergänzung ist nicht ausdrücklich freigegeben worden,
sondern eine Folgerung aus OPEN-B.

---

## D-21 – Zuweisbarkeit von Rollen (assignment_scope)

**Status:** ENTSCHIEDEN (2026-09-01)
**Setzt voraus:** D-05, D-17

`identity.role` erhält ein explizites Feld `assignment_scope` mit den Werten
`PLATFORM` und `ORGANIZATION`. Es legt fest, ob eine Rolle plattformweit oder
ausschließlich organisationsgebunden zuweisbar ist.

**Ausschließlich `PLATFORM_ADMIN` ist plattformweit zuweisbar.** Die übrigen
neun Rollen sind organisationsgebunden:

```text
PLATFORM       PLATFORM_ADMIN
ORGANIZATION   ORGANIZATION_ADMIN  PRODUCER    PROCESSOR   QUALITY_MANAGER
               LABORATORY          BOTTLER     LOGISTICS   RETAILER
               AUDITOR
```

`AUDITOR` ist bewusst **organisationsgebunden**. Ein plattformweiter Auditor
hätte organisationsübergreifenden Lesezugriff auf Fachdaten und würde D-02
und D-05 berühren; das ist nicht entschieden.

**Zweck:** Das Feld verhindert strukturell, dass `PLATFORM_ADMIN` einer
einzelnen Organisation zugewiesen wird oder `PRODUCER` plattformweit.

---

## D-22 – Membership- und Rollenzuweisungsmodell

**Status:** ENTSCHIEDEN (2026-09-01)
**Setzt voraus:** D-05, D-21
**Setzt voraus:** D-11 (Cross-Schema-Fremdschlüssel), entschieden

Mitgliedschaft und Rollenzuweisung sind **unterschiedliche Konzepte**. Ein
Benutzer darf Mitglied einer Organisation sein, ohne dass bereits eine Rolle
existiert. Das ER-Diagramm kennt nur `identity.user_role`; dieses Modell
ersetzt es durch drei getrennte Strukturen.

**`identity.organization_membership`**
Bildet die Mitgliedschaft eines Benutzers in einer Organisation ab.
`user_id` und `organization_id` sind verpflichtend. Ausdrücklich getrennt von
Rollen.

**`identity.organization_role_assignment`**
Verweist auf eine Mitgliedschaft und enthält `role_id`. `location_id` ist
optional und bedeutet ausschließlich eine Einschränkung der Zuweisung auf
einen Standort **innerhalb derselben Organisation**. Ist `location_id` gesetzt,
muss der Standort zu genau dieser Organisation gehören. `location_id = NULL`
bedeutet organisationsweiter Geltungsbereich innerhalb dieser einen
Organisation — **niemals Plattformzugriff**.
Zuweisbar sind hier ausschließlich Rollen mit
`assignment_scope = ORGANIZATION` (D-21).

**`identity.platform_role_assignment`**
Enthält `user_id` und `role_id`, **keinerlei `organization_id`**. Zuweisbar
sind ausschließlich Rollen mit `assignment_scope = PLATFORM` (D-21).

**Begründung:** D-05 verlangt, dass plattformweite und organisationsgebundene
Zuweisungen getrennt modelliert werden. Getrennte Tabellen setzen das
strukturell durch: eine organisationsübergreifende Zuweisung ist nicht
darstellbar, statt nur durch eine Prüfung verboten zu sein.

**Erledigt:** Der Bezug von `identity.organization_membership` auf
`org.organization` ist ein schemaübergreifender Fremdschlüssel. Die dafür
nötige Regel ist mit D-11 entschieden, ID-004 ist umgesetzt.

---

## D-23 – Umfang der ASP.NET Core Identity-Nutzung

**Status:** ENTSCHIEDEN (2026-09-01)
**Betrifft:** ID-005a, ID-005b, ID-006
**Präzisiert:** D-13

Das **Datenmodell** von ASP.NET Core Identity wird nicht verwendet:

- kein `IdentityUser` und keine davon abgeleitete Entität
- kein `UserManager`, kein `SignInManager`, kein `RoleManager`
- keine `AspNet*`-Tabellen

Maßgeblich bleibt das eigene Modell aus D-16 bis D-22.

Verwendet werden darf der Passwort-Hasher `IPasswordHasher<T>` aus
`Microsoft.Extensions.Identity.Core` — **ausschließlich als Implementierung in
der Infrastructure-Schicht**. Sein Namespace ist `Microsoft.AspNetCore.Identity`,
und `TypeDependencyArchitectureTests` verbietet Abhängigkeiten auf
`Microsoft.AspNetCore.*` in Domain und Application. Die Abstraktion dafür wird
erst eingeführt, wenn sie gebraucht wird, nicht vorab.

**Begründung:** D-13 nennt als ersten Punkt „ASP.NET Core Identity“, ohne den
Umfang zu benennen. Als vollständiges Datenmodell gelesen stünde das im
Widerspruch zur Auswirkung von D-13 selbst — dort ist festgehalten, dass
Credentials nicht zur Kernidentität gehören — und zum eigenen Rollenmodell.

---

## D-24 – Token- und Login-Parameter

**Status:** ENTSCHIEDEN (2026-09-01)
**Betrifft:** ID-005b

- Access Token (JWT): **15 Minuten** Gültigkeit
- Refresh Token: **14 Tage** Gültigkeit
- Rotation bei **jeder** erfolgreichen Verwendung eines Refresh Tokens
- Der JWT enthält Identität, aber **keine Rollen, Permissions oder
  Organizations**. Berechtigungen werden serverseitig aufgelöst.
- Login liefert für unbekannten Benutzer und für falsches Passwort
  **denselben** Fehler.
- Login erhält einen deutlich strengeren Rate-Limit-Schutz als der globale
  Limiter, ausdrücklich **nicht rein IP-basiert**.
- Ein fehlender oder ungültiger JWT-Signaturschlüssel führt beim Start zu
  **Fail Fast**. Es gibt keinen eingebauten Ersatzschlüssel.

**Begründung:** Bei 15 Minuten Gültigkeit bliebe ein Rechteentzug bis zu
15 Minuten wirkungslos, wenn Berechtigungen im Token stünden. Für den
Signaturschlüssel gibt es keinen sicheren Ersatzbetrieb: ein eingebauter
Standardwert erlaubte das Fälschen beliebiger Token.

---

## D-25 – Startverhalten bei fehlender Datenbankkonfiguration

**Status:** OFFEN
**Betrifft:** Betrieb und Deployment der API

Seit FIX-001 startet die API auch ohne konfigurierten Connection String und
meldet `/health/ready` dauerhaft als `Unhealthy`. Offen ist, ob das so bleibt
oder ob eine fehlende Datenbankkonfiguration den Start verhindern soll.
Praktische Wirkung entsteht erst mit der Containerisierung der API.

**Abgrenzung:** D-24 legt für den JWT-Signaturschlüssel bereits Fail Fast fest.
Das präjudiziert diese Entscheidung nicht: dort gibt es keinen sicheren
Ersatzbetrieb, bei fehlender Datenbank gibt es mit „nicht bereit“ dagegen einen
definierten Zustand.

---

## D-26 – Organization Context in API Routes

**Status:** ENTSCHIEDEN (2026-09-02)
**Betrifft:** ID-006 und alle folgenden tenantgebundenen Endpunkte
**Setzt voraus:** D-04, D-05, D-06, D-20, D-22, D-24

Tenant- beziehungsweise organisationsgebundene API-Ressourcen führen den
Organisationskontext **explizit im Ressourcenpfad**:

```text
/api/v1/organizations/{organizationId}/...
```

- Der Organisationskontext wird **nicht** über einen Header wie
  `X-Organization-Id` transportiert.
- Bei tenantgebundenen Listen und Neuanlagen wird er **nicht ausschließlich aus
  der Ressource abgeleitet**.
- Plattformweite Endpunkte sind ausgenommen, insbesondere `/api/v1/auth/*` und
  `/api/v1/me`.

**Begründung:** D-20 verlangt, dass eine Permission allein niemals
organisationsübergreifenden Zugriff ermöglicht, und D-05 begründet die
Trennung damit, dass implizite Sicherheitsbedingungen eine häufige Quelle
stiller Autorisierungsfehler sind. Ein Kontext im Pfad ist Pflichtbestandteil des
Vertrags und kann nicht vergessen werden; ein Header oder eine Ableitung aus der
geladenen Ressource kann stillschweigend fehlen und liefert dann bereits fremde
Daten, bevor geprüft wird.

**Auswirkung:** Die Pfadbeispiele in `AGENTS.md` §30 und in der Master
Specification sind flach (`/api/v1/lots`, `/api/v1/organizations`) und
widersprechen dieser Entscheidung für tenantgebundene Ressourcen. Sie werden in
einem separaten Dokumentations-Task nachgezogen, nicht nebenbei in einem
Implementierungstask.

---

## D-27 – Platform Permissions ohne Zugriff auf Organisationsressourcen

**Status:** ENTSCHIEDEN (2026-09-02)
**Betrifft:** ID-006 und alle folgenden tenantgebundenen Endpunkte
**Setzt voraus:** D-05, D-18, D-20, D-21, D-22, D-26

Permissions aus `identity.platform_role_assignment` gelten **ausschließlich im
Platform Scope** und gewähren keinen Zugriff auf organisationsgebundene
Ressourcen.

Für `/api/v1/organizations/{organizationId}/...` muss die erforderliche
Permission aus einer gültigen Organization Role Assignment für **genau diese
Organisation** und den erforderlichen Location Scope stammen.

Gleiche Permission-Codes dürfen in unterschiedlichen Scopes vorkommen. **Der
Permission-Code allein entscheidet nicht über Zugriff.** Entscheidend ist immer:

```text
Identity + Assignment Scope + Organization/Location Scope + Permission
```

**Auswirkung:** PlatformAdmin erhält dadurch keinen impliziten Zugriff auf
fachliche Kundendaten. Das in D-20 zugewiesene `organization.read` ist damit
nicht über `/api/v1/organizations/{organizationId}` nutzbar; plattformweite
Funktionen erhalten später eigene, explizite Platform-Endpunkte, etwa
`/api/v1/platform/organizations`.

Ein zukünftiger Support-, Impersonation- oder Emergency-Access-Mechanismus
wäre eine **eigene, explizite und auditierbare Funktion** — keine Ausnahme
durch normale PlatformAdmin-Permissions.

**Begründung:** D-20 verlangt, dass eine Permission allein niemals
organisationsübergreifenden Zugriff ermöglicht, und hält für PlatformAdmin
ausdrücklich fest, dass er bewusst ohne Zugriff auf fachliche Kundendaten
bleibt. Ohne diese Entscheidung erschiene der Widerspruch zwischen D-20 und dem
Verhalten der API als Fehler und würde vermutlich stillschweigend „repariert“.

---

## D-28 – Kein physischer `traceable_object`-Supertyp in Pilot 1

**Status:** ENTSCHIEDEN (2026-09-02)
**Beantwortet:** D-03
**Betrifft:** TRC-002, TRC-007, LOG-002, LOG-004, PUB-001

Pilot 1 besitzt physisch **nur `trace.lot`**. Eine Tabelle
`trace.traceable_object` wird **nicht** angelegt.

**Die Lot-Id ist zugleich die künftige Traceable-Object-Id.** Es gibt keine
zweite GUID und keinen zweiten Schlüsselraum.

Generische Trace- und Public-Verträge verwenden `traceableObjectId`.
Lot-spezifische Persistenz darf weiterhin `lot_id` heißen.

**Begründung:** Der Supertyp trägt für Pilot 1 nichts — die Olivenoelkette ist
durchgehend lotbasiert. Sein Wert entsteht erst bei Domänen mit individueller
Identität. Weil die Id identisch bleibt, ist die spätere Einführung additiv:
Supertyp-Tabelle anlegen, eine Zeile je Lot unter derselben Id, Fremdschlüssel
umhängen. Alle bestehenden Ids bleiben gültig, **einschließlich gedruckter
QR-Codes**, die nicht zurückgerufen werden können.

**Dokumentations-Finding:** Das ER-Diagramm führt `trace.traceable_object` und
referenziert an sieben Stellen `traceable_object_id`. Das widerspricht dieser
Entscheidung für die Persistenz. In `AGENTS.md` und `ARCHITECTURE.md` kommt der
Begriff nicht vor; Master Specification 6.2 verwendet `lot_id`. Das Diagramm ist
in einem eigenen Dokumentations-Task nachzuziehen.

---

## D-29 – Lot-Eigentum beim Organisationsuebergang

**Status:** ENTSCHIEDEN (2026-09-02)
**Beantwortet:** D-01
**Setzt voraus:** D-04, D-26, D-27
**Betrifft:** TRC-003, TRC-008, LOG-003, ORG-004

Ein Lot gehört **dauerhaft genau einer Organisation**; seine `organization_id`
ist **unveränderlich**.

Bei einem organisationsuebergreifenden Übergang entsteht beim Empfänger ein
**neues Lot**. Sender- und Empfänger-Lot werden explizit durch die
Traceability-Lineage verbunden.

Ausgeschlossen sind: das Verschieben eines bestehenden Lots in eine andere
Organisation, und allgemeine Cross-Tenant-Lese-Grants.

**Begründung:** D-04, D-26 und D-27 machen Mandantengrenzen hart und explizit;
ID-006 setzt sie durch. Eine Zeile, die den Mandanten wechselt, würde dieses
Modell an seiner empfindlichsten Stelle unterlaufen — die Historie eines Lots
läge dann teilweise in einem Mandanten, dem sie nicht mehr gehört. Ein
Lese-Grant würde neben Membership und Assignment einen zweiten Zugriffspfad
schaffen und damit die Aussage von D-27 aufheben. Die gewählte Variante kommt
zudem ohne neue Mechanik aus: sie nutzt ausschließlich die Lineage, die ohnehin
existieren muss.

**Dokumentations-Finding:** Kein bestehendes Dokument legt die Semantik des
Empfangsvorgangs fest. Diese Entscheidung schließt die Lücke.

---

## D-30 – Cross-Organization Visibility: Partner View und Public View

**Status:** ENTSCHIEDEN (2026-09-02)
**Beantwortet:** D-02
**Setzt voraus:** D-29
**Betrifft:** TRC-010, TRC-011, TRC-016, PUB-003, PUB-004

Sichtbarkeit wird in **zwei getrennte Sichten** aufgeteilt.

**Partner View**
- volle eigene Daten
- Identität des **unmittelbaren** Geschäftspartners sichtbar
- weiter entfernte Organisationen dürfen als Trace-Schritte erscheinen, werden
  aber hinsichtlich Organisation und geschäftlich vertraulicher Informationen
  **anonymisiert**

**Public View**
- ausschließlich **explizit freigegebene** Daten gemäß Public-Trace- und
  Visibility-Konfiguration
- **keine automatische Vererbung** der Partner-Sichtbarkeit

**Traversal und Visibility bleiben technisch getrennt.** Intern darf der
vollständige Graph traversiert werden; die API gibt ausschließlich eine für den
jeweiligen Aufrufer **projizierte Sicht** zurück.

**Begründung:** Ein Handelspartner und ein Verbraucher, der einen QR-Code
scannt, brauchen unterschiedliche Regeln — für den Verbraucher ist die
Lieferantenliste einer Mühle noch heikler als für den Partner. Die Trennung von
Traversierung und Projektion hält die Sichtbarkeitsentscheidung an genau einer
Stelle: Lockern ist damit später additiv möglich, Zurücknehmen wäre es nicht.

**Dokumentations-Finding:** Master Specification 5.4 verweist auf eine
„relationship/trace disclosure rule“, die in keinem Dokument existiert. Diese
Entscheidung liefert sie.

---

## Nächste freie ID

`D-31`
