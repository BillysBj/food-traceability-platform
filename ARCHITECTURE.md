# ARCHITECTURE.md
# Food Traceability Platform – Architekturleitfaden

## 1. Ziel

Die Food Traceability Platform ist eine generische Plattform zur Rückverfolgbarkeit von Lebensmitteln. Pilot 1 ist Olivenöl. Später sollen Milch/Feta, Fleisch, Fisch, Obst/Gemüse und weitere Produktgruppen ergänzt werden, ohne den Plattform-Kern neu zu bauen.

**Leitregel:** Der Core bleibt lebensmittelunabhängig. Produktspezifische Besonderheiten gehören in Industry Modules oder Product Profiles.

## 2. Zielarchitektur

- Backend: C# / .NET / ASP.NET Core Web API
- ORM: Entity Framework Core
- Datenbank: PostgreSQL
- Frontend: React / Next.js / TypeScript
- Deployment: Docker
- API-Doku: OpenAPI / Swagger
- Tests: Unit, Integration, Architecture, Authorization
- Dokumente: Object Storage
- Architekturstil: Modularer Monolith, DDD-light, Domain Events in-process, Outbox-ready

Keine Microservices in Pilot 1.

## 3. Solution-Struktur

```text
FoodTraceability.sln
src/
├── FoodTraceability.Api
├── BuildingBlocks
└── Modules/
    ├── Identity
    ├── Organizations
    ├── Catalog
    ├── Traceability
    ├── Production
    ├── Quality
    ├── Logistics
    ├── Assets
    ├── Documents
    ├── Certifications
    ├── Recall
    ├── PublicTrace
    ├── Integrations
    ├── Audit
    ├── AI
    └── Industries/
        ├── OliveOil
        ├── Dairy
        ├── Meat
        ├── Seafood
        └── Produce

tests/
├── UnitTests
├── IntegrationTests
└── ArchitectureTests
```

Pilot 1 implementiert zunächst: Identity, Organizations, Catalog, Traceability, Quality, Documents, Logistics, PublicTrace, Audit und ein minimales OliveOil-Modul.

## 4. Modulverantwortung

- **Identity:** Benutzer, Rollen, Permissions, Authentifizierung, Autorisierung
- **Organizations:** Firmen, Standorte, Mitgliedschaften, Tenant Scope
- **Catalog:** Kategorien, Produkte, Artikel, Einheiten
- **Traceability:** Lots/Chargen, Events, Inputs, Outputs, Forward/Backward Trace
- **Production:** Prozesse, Produktionsläufe, Rezepte, Prozessparameter
- **Quality:** Proben, Parameter, Laborwerte, Spezifikationen, Sperren/Freigaben
- **Logistics:** Transporte, Lieferungen
- **Assets:** Tanks, Maschinen, Kühlräume, Fahrzeuge, Sensorzuordnung
- **Documents:** Metadaten, Upload, Storage, Entity-Verknüpfungen
- **Certifications:** Zertifikate, Standards, Gültigkeiten
- **Recall:** Rückrufe, betroffene Chargen und Empfänger
- **PublicTrace:** QR/Public Token, Verbraucheransicht
- **Integrations:** ERP, LIMS, Behörden, GS1, externe APIs
- **Audit:** Wer hat was wann geändert?
- **AI:** Prognosen/Anomalien; nie Source of Truth

## 5. Datenbank

Eine PostgreSQL-Datenbank, z. B. `food_traceability`, mit mehreren Schemas:

```text
identity.*
org.*
catalog.*
trace.*
production.*
quality.*
logistics.*
asset.*
docs.*
certification.*
recall.*
publictrace.*
integration.*
audit.*
ai.*
olive.*
dairy.*
livestock.*
meat.*
seafood.*
produce.*
```

Jedes Modul besitzt seine Tabellen. Direkte Schreibzugriffe auf Tabellen anderer Module werden vermieden.

## 6. Abhängigkeiten

```text
API
 ↓
Application
 ↓
Domain

Infrastructure implementiert Datenbank- und externe Adapter.
```

Regeln:
- Domain kennt kein ASP.NET Core und kein EF Core.
- Controller enthalten keine Geschäftslogik.
- Application orchestriert Use Cases.
- DTOs sind keine EF Entities.
- Module kommunizieren über definierte Schnittstellen/Application Services/Domain Events.

## 7. Multi-Tenancy

Jeder geschützte Request berücksichtigt:

```text
User + Organization + optional Location + Role + Permissions
```

Organisation A darf interne Daten von B weder lesen noch verändern. Tenant-Isolation wird serverseitig erzwungen.

## 8. Rollen und Permissions

Beispielrollen: PlatformAdmin, OrganizationAdmin, Producer, Processor, QualityManager, Laboratory, Bottler, Logistics, Retailer, Auditor.

Beispielpermissions:

```text
lot.read
lot.create
lot.update
trace.read
trace.event.create
quality.read
quality.sample.create
quality.result.create
quality.release
quality.block
document.read
document.upload
shipment.read
shipment.create
user.read
user.manage
audit.read
```

Keine hartcodierten Rollenprüfungen; immer Permission + Scope.

## 9. Traceability Core

Der wichtigste Baustein ist generisch:

```text
Input Lot(s)
     ↓
Traceability Event
     ↓
Output Lot(s)
```

Beispiele:

```text
Oliven-Charge → PRESS → Öl-Charge → BOTTLE → Flaschen-Charge
```

```text
Milch A ─┐
         ├─ MIX → Tank-Charge
Milch B ─┘
```

```text
Schlachtkörper → CUT → mehrere Fleisch-Chargen
```

Zentrale Tabellen:

```text
trace.lot
trace.event_type
trace.traceability_event
trace.event_input
trace.event_output
```

Mindestens Eventtypen: HARVEST, RECEIVE, TRANSFER, STORE, PRESS, PROCESS, MIX, SPLIT, SAMPLE, QUALITY_RELEASE, BLOCK, UNBLOCK, BOTTLE, PACK, SHIP, DELIVER, SELL, RETURN, DISPOSE.

## 10. Product Profiles

Ein Product Profile definiert pro Produktgruppe:
- erlaubte/erforderliche Events
- Pflichtattribute
- Qualitätsparameter
- Pflichtdokumente
- Validierungen
- öffentliche QR-Felder

Beispiele: EXTRA_VIRGIN_OLIVE_OIL, RAW_MILK, FETA, BEEF, SEA_BASS, TOMATO.

## 11. Flexible Attribute

Hybrides Modell:
- stabile, wichtige Felder als normale relationale Spalten
- flexible Eigenschaften über `attribute_definition` und `lot_attribute_value`

Beispiele: OLIVE_VARIETY, RIPENESS, MILK_FAT, FISH_SPECIES, CATCH_AREA.

Keine reine EAV-/JSON-Datenbank.

## 12. Quality Framework

Generisch:

```text
quality.sample
quality.parameter
quality.lab_result
quality.specification
quality.specification_parameter
quality.lot_block
```

Neue Lebensmittel erhalten neue Parameter/Spezifikationen, nicht neue Qualitäts-Frameworks.

## 13. Production Framework

Langfristig generisch:

```text
process_definition
process_version
process_step
production_order
production_run
recipe
recipe_version
recipe_component
process_parameter_definition
process_parameter_value
```

Rezept/Prozessdefinition = Soll. Traceability Event / Production Run = Ist.

## 14. Assets und Sensoren

Generische Assets: TANK, PRESS, PASTEURIZER, PACKAGING_LINE, COLD_ROOM, VEHICLE.

Später: `sensor_device`, `sensor_measurement`.

## 15. Public Trace

Öffentliche API:

```text
GET /api/public/v1/trace/{token}
```

Token ist zufällig und getrennt von internen IDs. Sichtbare Felder werden über Public Trace Profile gesteuert.

## 16. Audit

```text
Traceability = Was ist mit dem Produkt passiert?
Audit        = Wer hat Daten geändert?
```

Beides bleibt getrennt.

## 17. Domain Events / Outbox

Sinnvolle Events: LotCreated, LotBlocked, LotReleased, TraceabilityEventCreated, QualityResultRecorded, DeliveryCreated.

Pilot 1 in-process; Architektur bleibt Outbox-ready.

## 18. GS1 / EPCIS Readiness

Die Architektur soll später auf EPCIS-Konzepte mapbar bleiben: What, When, Where, Why, How. Keine vollständige EPCIS-Implementierung in Pilot 1.

## 19. AI

AI ist nie Source of Truth. Fakten und Prognosen werden getrennt gespeichert.

Beispiel:

```text
Actual Output = 342 L
Predicted Output = 356 L
```

Später: Yield Forecast, Quality Risk, Anomaly Detection, Demand Forecast, Cold Chain Risk, Document Extraction.

## 20. Wichtigste Modulregel

Bei jeder neuen Anforderung zuerst fragen:

```text
Kann der bestehende Core das bereits mit Lot + Event + Input + Output abbilden?
```

Wenn ja: Core nicht ändern. Wenn nein: prüfen, ob es Industry Module, Production, Quality, Assets, Logistics oder wirklich ein neuer generischer Core-Baustein ist.

## 21. Nicht in Pilot 1

Kein komplettes ERP, keine Buchhaltung, kein CRM, keine Blockchain, keine Microservices, kein Kubernetes, keine vollständige IoT-/AI-/Behördenplattform und kein Inspection Portal.

## 22. Architecture Definition of Done

Eine Änderung ist akzeptabel, wenn:
- Modulgrenzen eingehalten sind
- keine unerlaubte Datenbankkopplung entsteht
- der Core produktneutral bleibt
- Organization Scope + Permissions geprüft werden
- DB-Constraints vorhanden sind
- Tests vorhanden sind
- keine unnötige Komplexität eingeführt wurde
- OpenAPI/Dokumentation aktuell sind

> Eine Plattform, ein stabiler Core, viele Produktgruppen.


---

## Swagger / OpenAPI – verbindlich

Das Backend verwendet Swagger verbindlich.

Technik:

```text
Swashbuckle.AspNetCore
```

Anforderungen:

- OpenAPI-Dokument automatisch aus der ASP.NET Core API erzeugen
- Swagger UI in der Entwicklungsumgebung aktivieren
- alle API-Endpunkte in Swagger sichtbar machen
- Request- und Response-DTOs dokumentieren
- HTTP-Statuscodes dokumentieren
- `ProblemDetails` und Validierungsfehler dokumentieren
- JWT/Bearer Authentication in Swagger UI konfigurieren
- geschützte Endpunkte mit Security Requirement kennzeichnen
- XML-Dokumentationskommentare einbinden, soweit sinnvoll
- Swagger muss bei jeder API-Änderung aktuell bleiben

Standardpfade:

```text
/swagger
/swagger/index.html
/swagger/v1/swagger.json
```

Eine API-Aufgabe ist nicht abgeschlossen, wenn der neue oder geänderte Endpoint nicht korrekt in Swagger dokumentiert und testbar ist.

In Production wird Swagger UI nur bewusst und abgesichert aktiviert.


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
