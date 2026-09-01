# AGENTS.md
# Food Traceability Platform – Codex Implementation Guide

## 1. Zweck dieses Dokuments

Dieses Dokument ist die verbindliche Arbeitsanweisung für Codex und alle Entwickler, die an der Food Traceability Platform arbeiten.

Die Plattform soll schrittweise mehrere Lebensmittelgruppen unterstützen. Pilot 1 ist Olivenöl. Später sollen unter anderem Milch/Feta, Fleisch, Fisch, Obst/Gemüse und weitere Produktgruppen ergänzt werden.

WICHTIG:
- Die Plattform darf architektonisch NICHT auf Olivenöl zugeschnitten werden.
- Pilot 1 ist der erste Anwendungsfall des generischen Plattform-Kerns.
- Der Traceability-Core muss lebensmittelunabhängig bleiben.
- Neue Produktgruppen sollen möglichst über Konfiguration und klar getrennte Industry Modules ergänzt werden.
- Keine unnötigen Microservices in der ersten Phase.

## Verbindlichkeit des Decision Logs

`docs/DECISIONS.md` ist die Source of Truth für explizite Architektur- und Modellentscheidungen. Bei Widersprüchen gilt eine dort als `ENTSCHIEDEN` geführte Entscheidung; als `OFFEN` geführte Entscheidungen dürfen nicht durch Implementierung vorweggenommen werden.

---

# 2. Technologie-Stack

Backend:
- C#
- .NET
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL

Frontend:
- React
- Next.js
- TypeScript

Infrastructure:
- Docker
- PostgreSQL
- Object Storage für Dokumente
- OpenAPI / Swagger UI mit `Swashbuckle.AspNetCore`
- Serilog oder vergleichbares strukturiertes Logging
- OpenTelemetry vorbereiten
- Health Checks

Tests:
- Unit Tests
- Integration Tests
- Architecture Tests
- API Tests
- Authorization Tests

---

# 3. Architekturprinzip

Verwende einen modularen Monolithen.

NICHT:
- sofort Microservices bauen
- Kafka/RabbitMQ erzwingen
- verteilte Systeme ohne Bedarf einführen
- Business Logic in Controller schreiben

Zielstruktur:

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

Pilot 1 benötigt zunächst nur:

- Identity
- Organizations
- Catalog
- Traceability
- Quality
- Documents
- Logistics
- PublicTrace
- Audit
- minimal OliveOil Industry Module

Andere Module sollen architektonisch vorbereitet, aber nicht unnötig vollständig implementiert werden.

---

# 4. Abhängigkeitsregeln

Grundprinzip:

```text
API
 ↓
Application
 ↓
Domain

Infrastructure implementiert Ports/Interfaces
```

Regeln:
- Domain kennt keine Datenbank.
- Domain kennt kein ASP.NET Core.
- Domain kennt kein EF Core.
- Controller enthalten keine Geschäftslogik.
- Application orchestriert Use Cases.
- Infrastructure enthält EF Core, externe APIs, Object Storage, Messaging Adapter usw.
- Module dürfen nicht beliebig direkt auf Tabellen anderer Module zugreifen.
- Kommunikation zwischen Modulen erfolgt bevorzugt über Application Services, Interfaces oder Domain Events.

---

# 5. Datenbank

Verwende PostgreSQL.

Schemas:

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

Jedes Modul besitzt seine eigenen Tabellen.

Kein Modul soll fremde Tabellen direkt verändern, wenn es über eine definierte Schnittstelle lösbar ist.

---

# 6. Multi-Tenancy / Organisationen

Die Plattform ist Multi-Organization-fähig.

Jeder relevante Request muss mindestens berücksichtigen:

```text
User
+
Organization
+
optional Location
+
Role
+
Permissions
```

Regeln:
- Organisation A darf interne Daten von Organisation B nicht lesen.
- Organisation A darf interne Daten von Organisation B nicht verändern.
- Ein Benutzer kann in mehreren Organisationen unterschiedliche Rollen besitzen.
- Standort-Scope kann optional zusätzlich gelten.
- Tenant-/Organization-Filter müssen serverseitig erzwungen werden.
- Niemals auf Frontend-Filter als Sicherheitsmechanismus verlassen.

---

# 7. Rollen und Permissions

Rollen:

```text
PlatformAdmin
OrganizationAdmin
Producer
Processor
QualityManager
Laboratory
Bottler
Logistics
Retailer
Auditor
```

Kanonische Permission-Liste für Pilot 1 gemäß D-18 in `docs/DECISIONS.md`:

```text
organization.read
organization.manage

user.read
user.manage

role.read
permission.read

product.read
product.create
product.update

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

transport.read
transport.create

delivery.read
delivery.create

audit.read
```

WICHTIG:
Nicht im Code hart verdrahten:

```csharp
if (user.Role == "Processor")
```

Stattdessen Permission-basiert autorisieren.

---

# 8. Traceability Core

Der Traceability Core ist das wichtigste Modul.

Er darf NICHT wissen, ob es sich um:
- Oliven
- Olivenöl
- Milch
- Feta
- Fleisch
- Fisch
- Gemüse
- Wein
- Honig
oder andere Lebensmittel handelt.

Zentrale Struktur:

```text
Lot
TraceabilityEvent
EventInput
EventOutput
```

Transformation:

```text
Input Lot(s)
     ↓
Traceability Event
     ↓
Output Lot(s)
```

Beispiele:

Olivenöl:

```text
OL-001
 ↓ PRESS
OIL-001
 ↓ BOTTLE
BOT-001
```

Milch:

```text
MILK-A ─┐
        ├─ MIX → MILK-TANK-001
MILK-B ─┘
```

Feta:

```text
MILK-001 ─────┐
CULTURE-01 ───┤
RENNET-01 ────┼─ PROCESS → FETA-001
SALT-01 ──────┘
```

Fleisch:

```text
CARCASS-001
      ↓ CUT
MEAT-001 + MEAT-002
```

Die gleiche Engine muss alle Fälle unterstützen.

---

# 9. Lot / Charge

Zentrale Tabelle:

```text
trace.lot
```

Mindestens:

```text
lot_id UUID PK
article_id UUID FK
organization_id UUID FK
location_id UUID FK
unit_id UUID FK

lot_number VARCHAR
quantity NUMERIC

production_date DATE
best_before_date DATE NULL

origin_country CHAR(2)

quality_status VARCHAR
status VARCHAR

notes TEXT NULL

created_at TIMESTAMPTZ
updated_at TIMESTAMPTZ
```

Regeln:
- quantity > 0
- lot_number eindeutig innerhalb der Organisation
- Business-relevante Lots nicht physisch löschen
- Statusänderungen nachvollziehbar machen
- Audit erzeugen

Unique Constraint:

```text
UNIQUE (organization_id, lot_number)
```

---

# 10. Traceability Event

```text
trace.traceability_event
```

Mindestens:

```text
event_id UUID PK
event_type_id UUID FK

organization_id UUID FK
location_id UUID FK

occurred_at TIMESTAMPTZ
external_reference VARCHAR NULL

status VARCHAR
description TEXT NULL

metadata JSONB NULL

created_by UUID FK
created_at TIMESTAMPTZ
```

Inputs:

```text
trace.event_input
```

Outputs:

```text
trace.event_output
```

Beide mit:

```text
event_id
lot_id
quantity
unit_id
```

---

# 11. Eventtypen

Mindestens vorbereiten:

```text
HARVEST
RECEIVE
TRANSFER
STORE
PRESS
PROCESS
MIX
SPLIT
SAMPLE
QUALITY_RELEASE
BLOCK
UNBLOCK
BOTTLE
PACK
SHIP
DELIVER
SELL
RETURN
DISPOSE
```

Industry Modules dürfen zusätzliche Eventtypen registrieren.

Keine eigene Tabelle je Prozess erstellen.

NICHT:

```text
pressing_table
harvest_table
bottling_table
```

wenn der Vorgang durch generische Events abbildbar ist.

---

# 12. Forward / Backward Traceability

Muss rekursiv funktionieren.

Backward:

```text
Bottle Lot
 ↓
Oil Lot
 ↓
Olive Lot
 ↓
Producer
```

Forward:

```text
Olive Lot
 ↓
Oil Lot
 ↓
Bottle Lots
 ↓
Deliveries
 ↓
Retailers
```

Erforderliche Endpunkte:

```text
GET /api/v1/lots/{id}/traceability/backward
GET /api/v1/lots/{id}/traceability/forward
```

Anforderungen:
- Zyklen erkennen/verhindern
- Duplicate Nodes vermeiden
- große Graphen performant verarbeiten
- Organization-/Permission-Filter beachten
- Ergebnisse als Graph-Struktur zurückgeben

Für komplexe Graph-Abfragen darf später Dapper oder Raw SQL ergänzt werden.

---

# 13. Product Profiles

Neue Produktgruppen sollen möglichst wenig Codeänderungen am Core verlangen.

Ein Product Profile definiert zum Beispiel:

```text
Product Type
Required Attributes
Allowed Events
Required Quality Parameters
Required Documents
Validation Rules
Public Trace Fields
```

Beispiele:

```text
EXTRA_VIRGIN_OLIVE_OIL
RAW_MILK
FETA
BEEF
SEA_BASS
TOMATO
```

Product Profiles sollen konfigurierbar sein, aber keine komplette No-Code-Engine werden.

---

# 14. Flexible Attribute

Nicht alle fachlichen Daten gehören in `lot`.

Verwende für variable Eigenschaften:

```text
attribute_definition
attribute_value
```

Beispiele:

Olive Oil:

```text
OLIVE_VARIETY = Koroneiki
RIPENESS = 3.8
MOISTURE = 47 %
```

Milk:

```text
SPECIES = Sheep
FAT = 6.2 %
PROTEIN = 5.4 %
```

Fish:

```text
SPECIES = Sea Bream
CATCH_AREA = FAO 37
PRODUCTION_METHOD = Aquaculture
```

Regel:
Wenn ein Feld zentrale Geschäftslogik, häufige Queries oder regulatorisch wichtige Beziehungen trägt, erhält es eine echte relationale Struktur.

Nicht alles als Key/Value modellieren.

---

# 15. Production Module

Langfristig generisch.

Tabellen / Aggregate können umfassen:

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

Soll-Prozess und Ist-Prozess unterscheiden.

Rezept:
= Soll

Traceability Event / Production Run:
= Ist

---

# 16. Prozessparameter

Generisch modellieren.

Beispiele:

Olivenöl:

```text
PRESS
Temperature = 26 °C
Duration = 35 min
```

Milch:

```text
PASTEURIZATION
Temperature = 72 °C
Duration = 20 sec
```

Fisch:

```text
FREEZING
Temperature = -35 °C
Duration = 4 h
```

Nicht pro Lebensmittel neue technische Parameter-Tabellen anlegen.

---

# 17. Quality Module

Generische Tabellen:

```text
quality.sample
quality.parameter
quality.lab_result
quality.specification
quality.specification_parameter
quality.lot_block
```

Pilot 1 Beispiele:

```text
Free Acidity
Peroxide Value
K232
K270
```

Dairy:

```text
Fat
Protein
Lactose
Somatic Cell Count
Bacterial Count
Antibiotic Residues
```

Meat:

```text
pH
Microbiology
Temperature
Residue Tests
```

Neue Produktgruppen sollen überwiegend neue Parameter und Spezifikationen konfigurieren können.

---

# 18. Qualitätsstatus

Mindestens:

```text
PENDING
PASS
FAIL
BLOCKED
RELEASED
```

Regeln:
- BLOCKED lot darf nicht ausgeliefert werden.
- RELEASE nur mit entsprechender Permission.
- jede Blockierung/Freigabe auditieren.
- Quality Release kann später Domain Event erzeugen.

---

# 19. Documents

Metadaten in PostgreSQL.

Dateien in Object Storage.

Nicht große PDF/Bild-Binärdaten standardmäßig in PostgreSQL speichern.

Tabellen:

```text
docs.document_type
docs.document
docs.document_link
```

Dokumente können mit mehreren Entities verknüpft werden, z. B.:

```text
Lot
Sample
Organization
Delivery
Certificate
```

---

# 20. Logistics

Generische Tabellen:

```text
logistics.transport
logistics.transport_item

logistics.delivery
logistics.delivery_item
```

Ziel:
Beantworten können:

```text
Welche Charge wurde wann,
von wem,
wohin,
in welcher Menge geliefert?
```

---

# 21. Assets

Generisch modellieren.

```text
asset.asset
asset.asset_type
```

Mögliche Typen:

```text
TANK
PRESS
PASTEURIZER
PACKAGING_LINE
COLD_ROOM
SILO
VEHICLE
```

Keine getrennten Tabellen nur für:

```text
olive_tank
milk_tank
wine_tank
```

wenn dieselbe generische Struktur ausreicht.

---

# 22. IoT / Sensoren

Später:

```text
sensor_device
sensor_measurement
```

Messwerte:

```text
Temperature
Humidity
Pressure
Weight
pH
Energy
```

Sensoren können Asset, Transport, Location oder Lot zugeordnet werden.

Pilot 1: vorbereiten, nicht zwingend vollständig implementieren.

---

# 23. Public Trace / QR

Öffentliche QR-Endpunkte dürfen keine internen sensitiven IDs offenlegen.

Beispiel:

```text
GET /api/public/v1/trace/{publicToken}
```

Public Token:
- zufällig
- nicht erratbar
- separat von internen IDs
- deaktivierbar

Public Trace kann zeigen:

```text
Product
Origin
Variety
Harvest Date
Producer
Processor
Quality Status
Certificates
Lot Number
```

Welche Felder sichtbar sind, wird über Public Trace Profile gesteuert.

Endkunde benötigt Pilot 1 keinen Account.

---

# 24. Audit

Traceability und Audit NICHT vermischen.

Traceability:
= Was ist mit dem Produkt passiert?

Audit:
= Wer hat Daten verändert?

Audit-Tabelle:

```text
audit.audit_log
```

Mindestens:

```text
audit_id
organization_id
user_id

entity_type
entity_id

action

before_data JSONB
after_data JSONB

change_reason
request_id
created_at
```

Geschäftskritische Audit-Einträge nicht normal löschbar machen.

---

# 25. Recall

Recall ist zunächst kein eigener komplexer Workflow.

Aber der Traceability Core muss bereits ermöglichen:

```text
Problem Lot
 ↓
alle Outputs
 ↓
alle Folge-Lots
 ↓
alle Lieferungen
 ↓
betroffene Empfänger
```

Später eigenes Recall Module.

---

# 26. Industry Module: Olive Oil

Pilot 1.

Zusätzliche fachliche Daten können umfassen:

```text
Olive Variety
Field / Parcel
Harvest Method
Ripeness
Moisture
Malaxation Duration
Malaxation Temperature
Extraction Method
Oil Yield
Oil Tank
```

WICHTIG:
Olivenöl-spezifische Logik niemals in generischen Core-Code mischen.

---

# 27. Industry Module: Dairy

Später ergänzen:

```text
Herd
Species
Breed
Milking
Milk Collection
Milk Tank
Cooling
Pasteurization
Cheese Production
Maturation
```

Mögliche eigene Tabellen:

```text
dairy.herd
dairy.milking_session
```

sowie generische Assets/Tanks.

---

# 28. Industry Module: Meat

Später ergänzen:

```text
livestock.animal
livestock.herd
livestock.species
livestock.breed
livestock.holding
```

Meat-Prozesse:

```text
SLAUGHTER
CHILL
CUT
PROCESS
PACK
```

Einzeltier-Identität nicht einfach als beliebiges Attribute speichern, wenn sie Kern der Fachlogik ist.

---

# 29. GS1 / EPCIS Readiness

Interne Architektur soll konzeptionell auf GS1/EPCIS mapbar bleiben.

Wichtige Konzepte:

```text
What
When
Where
Why
How
```

Transformation:

```text
Input
 ↓
Transformation Event
 ↓
Output
```

Nicht sofort vollständige EPCIS-Komplexität implementieren.

Aber keine Architektur bauen, die später EPCIS-Mapping verhindert.

GS1 Digital Link für QR/Public Trace langfristig berücksichtigen.

---

# 30. API Regeln

REST-API.

Beispiele:

```text
POST /api/v1/auth/login

GET  /api/v1/organizations
POST /api/v1/organizations

GET  /api/v1/locations
POST /api/v1/locations

GET  /api/v1/products
POST /api/v1/products

GET  /api/v1/articles
POST /api/v1/articles

GET  /api/v1/lots
POST /api/v1/lots
GET  /api/v1/lots/{id}

POST /api/v1/traceability/events

GET /api/v1/lots/{id}/traceability/backward
GET /api/v1/lots/{id}/traceability/forward

POST /api/v1/samples
POST /api/v1/lab-results

POST /api/v1/lots/{id}/block
POST /api/v1/lots/{id}/release

POST /api/v1/transports
POST /api/v1/deliveries

POST /api/v1/documents

POST /api/v1/trace-codes

GET /api/public/v1/trace/{code}
```

Regeln:
- korrekte HTTP Status Codes
- Validation Problem Details
- konsistente Error Responses
- DTOs statt EF Entities direkt exponieren
- Pagination für Listen
- Filter/Sortierung klar definieren
- OpenAPI aktuell halten

---

# 31. Business Rules

Mindestens:

```text
quantity > 0
```

```text
blocked lot darf nicht ausgeliefert werden
```

```text
lot number pro Organisation eindeutig
```

```text
Output darf nicht auf logische Weise vor Input entstehen
```

```text
keine Traceability-Zyklen
```

```text
User darf nur innerhalb seines Organization Scopes agieren
```

```text
Quality Release nur mit Permission
```

```text
Audit bei kritischen Änderungen
```

Zusätzliche fachliche Regeln gehören ins jeweilige Industry Module oder Domain Layer.

---

# 32. Zeit / Datum

Intern:
- UTC
- TIMESTAMPTZ in PostgreSQL

Frontend:
- lokale Zeit darstellen

Keine lokale Serverzeit als fachliche Wahrheit verwenden.

---

# 33. IDs

Bevorzugt UUID für fachliche Entities.

Keine öffentlichen QR URLs mit internen IDs.

Lot Nummern sind Business Identifiers.

---

# 34. Soft Delete

Geschäfts- und Traceability-Daten nicht physisch löschen.

Verwende:
- Status
- archived_at
- is_active

Hard Delete nur für wirklich unkritische Konfigurationsdaten und nur wenn fachlich erlaubt.

---

# 35. Nummernkreise

Chargennummern nicht zwingend manuell erzeugen.

Vorbereiten:

```text
OL-2026-000001
OIL-2026-000001
BOT-2026-000001
```

Nummernkreis-Konzept soll später konfigurierbar sein.

---

# 36. Datenbank Constraints

Wichtige Integrität auch in PostgreSQL erzwingen.

Verwende:
- NOT NULL
- UNIQUE
- FOREIGN KEY
- CHECK
- INDEX

Nicht nur auf C#-Validierung verlassen.

---

# 37. Performance / Indizes

Mindestens Indizes auf:

```text
trace.event_input(lot_id)
trace.event_output(lot_id)

trace.traceability_event(occurred_at)
trace.traceability_event(organization_id)

trace.lot(lot_number)
trace.lot(organization_id)

logistics.delivery_item(lot_id)

publictrace.trace_code(public_token)
```

Query-Pläne bei Traceability-Graphen beobachten.

---

# 38. Security

Mindestens:

- HTTPS
- sichere Password Hashes
- Authentication
- RBAC / Permission Authorization
- Organization Scope
- Rate Limiting
- Input Validation
- sichere Datei-Uploads
- Secrets nicht im Repository
- Audit Logging
- Security Headers
- CORS restriktiv konfigurieren

Optional später:
- 2FA
- OIDC
- SSO

---

# 39. Logging / Observability

Verwende strukturiertes Logging.

Jeder Request sollte eine Correlation/Request ID erhalten.

Vorbereiten:
- Logs
- Metrics
- Traces
- Health Checks

PII und Secrets nicht unkontrolliert loggen.

---

# 40. Backups

Vorsehen:

- PostgreSQL Backups
- Object Storage Backups
- Restore Tests

Ein Backup gilt erst als zuverlässig, wenn Restore getestet wurde.

---

# 41. AI / Machine Learning

AI ist NICHT Source of Truth.

Trenne:

```text
Actual Value
```

von:

```text
Predicted Value
```

Beispiel:

```text
Actual Oil Output = 342 L
Predicted Oil Output = 356 L
```

Mögliche spätere AI-Funktionen:

```text
Oil Yield Prediction
Milk Yield Prediction
Quality Prediction
Anomaly Detection
Demand Forecasting
Production Planning
Cold Chain Risk
Recall Risk Prioritization
Document Extraction
Natural Language Assistant
```

Pilot 1:
hauptsächlich AI-ready Daten sammeln.

---

# 42. AI-ready Daten

Von Anfang an sauber historisieren, soweit verfügbar:

```text
Input Quantity
Output Quantity
Variety
Origin
Harvest Date
Moisture
Ripeness
Temperature
Process Duration
Storage Duration
Transport Duration
Machine / Asset
Lab Results
Quality Status
Yield
```

Nicht alle Felder sind Pflicht.

Aber wenn Daten fachlich existieren, sollen sie strukturiert speicherbar sein.

---

# 43. AI Predictions

Später generische Struktur möglich:

```text
ai.prediction
```

Beispiel:

```text
prediction_id
model_id

entity_type
entity_id

prediction_type

predicted_value
confidence

created_at
```

Prediction niemals stillschweigend als tatsächlichen Messwert speichern.

---

# 44. Dokumenten-KI

Später:
- Laborberichte auslesen
- Lieferscheine auslesen
- Zertifikate extrahieren

Workflow:

```text
Upload
 ↓
AI Extraction
 ↓
User Review
 ↓
Confirm
 ↓
Structured Data
```

Keine ungeprüfte KI-Extraktion automatisch als regulatorische Wahrheit behandeln.

---

# 45. Frontend Pilot 1

Mindestens folgende Screens:

```text
Login
Dashboard

Organizations
Locations

Users
Roles
Permissions

Products
Articles

Lots
Lot Detail

Traceability Event Create
Traceability Graph

Quality
Samples
Lab Results
Block / Release

Documents

Transport
Delivery

QR Codes

Public Consumer Page

Audit
```

---

# 46. Lot Detail Screen

Mindestens:

```text
Lot Number
Product
Quantity
Unit
Status
Quality Status
Origin
Production Date

Traceability Graph
Quality Results
Documents
Logistics
Audit History
```

---

# 47. Testing

Pflicht:

Unit Tests:
- Domain Rules
- Validation
- Traceability Logic

Integration Tests:
- PostgreSQL
- EF Core
- API
- Auth
- Organization Scope

Architecture Tests:
- keine unerlaubten Modulabhängigkeiten

Traceability Test:

```text
OL-001
 ↓ PRESS
OIL-001
 ↓ BOTTLE
BOT-001
```

Erwartung:

```text
Backward(BOT-001)
```

enthält:

```text
OIL-001
OL-001
```

und:

```text
Forward(OL-001)
```

enthält:

```text
OIL-001
BOT-001
```

Zusätzlich Mischfall testen:

```text
OL-001 ─┐
        ├─ PRESS → OIL-001
OL-002 ─┘
```

---

# 48. Pilot 1 Testdaten

Beispiel:

```text
3 Producers
2 Farms
1 Olive Mill
1 Laboratory
1 Bottler
1 Logistics Company
2 Retailers

20 Olive Lots
8 Oil Lots
15 Bottle Lots
5 Samples
10 Deliveries
```

Seed-Daten nur in Development/Test.

---

# 49. Acceptance Criteria Pilot 1

Pilot 1 ist abgeschlossen, wenn folgende End-to-End-Kette real funktioniert:

```text
Producer
 ↓
Harvest
 ↓
Olive Lot
 ↓
Mill Receipt
 ↓
Pressing
 ↓
Oil Lot
 ↓
Sample
 ↓
Lab Result
 ↓
Release
 ↓
Bottling
 ↓
Bottle Lot
 ↓
Delivery
 ↓
Retailer
 ↓
QR
 ↓
Public Trace
```

Zusätzlich muss intern funktionieren:

```text
Problem Lot
 ↓
Forward Trace
 ↓
Affected Lots
 ↓
Deliveries
 ↓
Retailers
```

und:

```text
Bottle Lot
 ↓
Backward Trace
 ↓
Oil Lot
 ↓
Olive Lots
 ↓
Producer
```

---

# 50. Nicht in Pilot 1 bauen

Nicht unnötig implementieren:

```text
komplettes ERP
Accounting
Invoices
CRM
Blockchain
Microservices
Kubernetes
vollständige IoT Plattform
vollständige Behördenintegration
komplexes ESG
vollständige AI Suite
alle Lebensmittel gleichzeitig
```

Architektur darf spätere Ergänzung ermöglichen.

---

# 51. Implementierungsreihenfolge für Codex

Codex soll NICHT die gesamte Plattform in einem unkontrollierten Schritt erzeugen.

Arbeite in kleinen, reviewbaren Phasen.

## Phase 0 – Repository Setup

Erstellen:

```text
Solution
Projects
Folder Structure
Docker Compose
PostgreSQL
Configuration
Logging
OpenAPI
Health Checks
Test Projects
```

Keine Fachlogik.

---

## Phase 1 – Identity

Implementieren:

```text
Users
Roles
Permissions
UserRole
RolePermission
Authentication
Authorization
```

Tests inklusive Organization Scope vorbereiten.

---

## Phase 2 – Organizations

Implementieren:

```text
Organization
Location
Organization Membership
```

CRUD + Permissions + Tests.

---

## Phase 3 – Catalog

Implementieren:

```text
ProductCategory
Product
Article
Unit
```

CRUD + Validierungen.

---

## Phase 4 – Traceability Core

Implementieren:

```text
Lot
EventType
TraceabilityEvent
EventInput
EventOutput
```

Danach:
- Create Lot
- Create Event
- Forward Trace
- Backward Trace
- Graph Tests

Dieser Schritt muss gründlich reviewed werden.

---

## Phase 5 – Quality

Implementieren:

```text
Sample
Parameter
LabResult
Specification
LotBlock
Release
```

Business Rules + Audit.

---

## Phase 6 – Documents

Implementieren:
- Upload
- Metadata
- Object Storage Interface
- Entity Links
- File Validation

---

## Phase 7 – Logistics

Implementieren:

```text
Transport
TransportItem
Delivery
DeliveryItem
```

Blocked Lot darf nicht versendet werden.

---

## Phase 8 – Public Trace

Implementieren:

```text
TraceCode
PublicTraceProfile
Public Trace API
QR Token
```

Keine internen sensitiven Daten offenlegen.

---

## Phase 9 – Audit

Vollständige Audit-Abdeckung für:
- Lot
- Quality
- User
- Organization
- Trace Events
- Delivery
- Public Trace Configuration

---

## Phase 10 – Olive Oil Pilot Module

Nur fachliche Erweiterungen:
- Olive attributes
- pressing parameters
- oil yield
- olive-specific quality configuration

Core nicht verändern, sofern nicht zwingend erforderlich.

---

## Phase 11 – Frontend

In derselben Reihenfolge:

```text
Auth
Organizations
Catalog
Lots
Traceability
Quality
Documents
Logistics
Public Trace
Audit
```

---

## Phase 12 – End-to-End Tests

Automatisierte Tests für reale Pilot-Szenarien.

---

# 52. Codex Arbeitsweise

Vor jeder Phase:

1. relevante Spezifikation lesen
2. bestehende Architektur analysieren
3. Implementierungsplan erstellen
4. nur Scope der aktuellen Phase ändern
5. Tests schreiben
6. Tests ausführen
7. Migrationen prüfen
8. Zusammenfassung der Änderungen liefern
9. offene Architekturentscheidungen explizit markieren

Codex darf keine wesentlichen Architekturentscheidungen stillschweigend ändern.

---

# 53. Regeln für Codex bei Unklarheiten

Wenn Spezifikation und bestehender Code widersprechen:

1. nicht blind überschreiben
2. Konflikt dokumentieren
3. bevorzugte Lösung begründen
4. nur ändern, wenn eindeutig

Wenn eine fachliche Regel fehlt:
- keine regulatorische Regel erfinden
- TODO / offene Entscheidung dokumentieren
- technische Struktur vorbereiten

---

# 54. Keine Overengineering-Regel

Bevor neue Abstraktion eingeführt wird, prüfen:

```text
Brauchen wir sie jetzt?
```

Vermeiden:
- unnötige Repository Layer über EF Core
- generische Base Classes für alles
- Event Bus ohne Use Case
- CQRS überall nur aus Prinzip
- komplizierte Reflection Frameworks
- Dynamic DSLs ohne Bedarf
- hunderte kleine Projects

Pragmatisches Domain-Driven Design.

---

# 55. EF Core Regeln

- klare Entity Configurations
- Fluent API bevorzugen
- Migrations versionieren
- keine Lazy Loading Proxies
- keine EF Entities direkt als API DTO verwenden
- N+1 Queries vermeiden
- CancellationToken nutzen
- Read Queries bei Bedarf `AsNoTracking`
- Transactions bewusst verwenden

---

# 56. C# Coding Standards

- Nullable Reference Types aktivieren
- async/await für I/O
- CancellationToken bis Infrastructure weitergeben
- DateTimeOffset für Zeitpunkte
- GUID/UUID IDs
- klare Namespaces
- keine Magic Strings
- Enums oder Value Objects wo sinnvoll
- kleine Services
- keine God Classes
- Methoden mit klarer Verantwortung

---

# 57. Domain Events

Vorbereiten und bei sinnvollen Business Events verwenden.

Beispiele:

```text
LotCreated
LotBlocked
LotReleased
TraceabilityEventCreated
DeliveryCreated
QualityResultRecorded
```

Pilot 1 zunächst in-process.

Outbox später oder bei echten externen Integrationen ergänzen.

---

# 58. Outbox Readiness

Architektur soll später Outbox Pattern erlauben.

Nicht zwingend sofort Message Broker einführen.

Möglicher späterer Ablauf:

```text
DB Transaction
├── Business Change
└── Outbox Message
```

danach:

```text
Outbox Worker
 ↓
External Message Broker
```

---

# 59. API Security Regeln

Jeder geschützte Endpunkt:
- Authentifizierung
- Permission
- Organization Scope
- Input Validation

Beispiel:

```text
POST /api/v1/lots
```

erfordert:

```text
lot.create
```

und Zugriff auf `organization_id`.

Nie Organization IDs aus Request ungeprüft vertrauen.

---

# 60. Frontend Security

Frontend darf Berechtigungen für UX verwenden.

Aber Backend bleibt alleinige Autorität.

Frontend-Hiding ist KEIN Sicherheitsmechanismus.

---

# 61. Fehlerbehandlung

Global Exception Handling.

Konsistente Problem Details.

Keine Stack Traces an Production Clients.

Validierungsfehler klar benennen.

---

# 62. Seed / Reference Data

Seed nur für:
- Event Types
- Units
- Default Roles
- Default Permissions
- Development Test Data

Produktionsdaten niemals als Migration Seed hardcoden.

---

# 63. Konfiguration

Environment-basierte Konfiguration.

Secrets:
- Environment Variables
- Secret Store

Nicht:
- `appsettings.json` mit echten Secrets committen

---

# 64. Definition of Done für jeden Codex Task

Ein Task gilt nur als fertig, wenn:

- Code kompiliert
- Tests erfolgreich
- keine offensichtlichen Security-Lücken
- Migrationen valide
- OpenAPI aktuell
- Organization Scope geprüft
- Permission geprüft
- Logging sinnvoll
- keine neue unerlaubte Modulabhängigkeit
- README/Docs aktualisiert wenn nötig
- offene Punkte dokumentiert

---

# 65. Erste konkrete Codex-Anweisung

Nachdem Repository, Master Specification und ER-Diagramm verfügbar sind, soll Codex zunächst NUR Folgendes tun:

```text
1. Analysiere AGENTS.md vollständig.
2. Analysiere die Master Specification.
3. Analysiere das Master ER Diagram.
4. Erstelle einen Implementierungsplan für Phase 0 bis Phase 4.
5. Erzeuge die Solution- und Projektstruktur.
6. Konfiguriere ASP.NET Core, PostgreSQL, EF Core, Docker Compose,
   OpenAPI, Logging, Health Checks und Testprojekte.
7. Implementiere noch NICHT alle Business Module.
8. Dokumentiere erkannte Inkonsistenzen zwischen Specification und ER Diagram.
9. Führe Build und Basistests aus.
10. Stoppe danach und liefere einen Review-Bericht.
```

Erst danach nächste Phase starten.

---

# 66. Zweite Codex-Anweisung

Nach erfolgreichem Review:

```text
Implementiere Identity, Organizations und Catalog vollständig.

Anforderungen:
- EF Core Entities und Configurations
- PostgreSQL Migrations
- DTOs
- Application Use Cases
- REST APIs
- Permissions
- Organization Scope
- Validation
- Audit-Basics
- Unit Tests
- Integration Tests
- OpenAPI

Keine Traceability-Implementierung in diesem Schritt.
```

---

# 67. Dritte Codex-Anweisung

Danach:

```text
Implementiere den Traceability Core.

Scope:
- Lot
- EventType
- TraceabilityEvent
- EventInput
- EventOutput
- Create Lot
- Create Traceability Event
- Forward Trace
- Backward Trace
- Graph Response
- Cycle Protection
- Permission Checks
- Organization Scope
- Database Constraints
- Performance Indices
- Audit Integration
- Unit Tests
- Integration Tests

Nutze generische Konzepte.
Keine OliveOil-spezifischen Felder in den Traceability Core aufnehmen.
```

---

# 68. Wichtigste Architekturregel

Wenn eine neue Lebensmittelgruppe hinzugefügt wird, stelle zuerst die Frage:

```text
Kann der bestehende Core diesen Prozess bereits
mit Lot + Event + Input + Output darstellen?
```

Wenn JA:
Core nicht ändern.

Wenn NEIN:
prüfen, ob es sich handelt um:
- Industry Module
- Production Feature
- Asset Feature
- Quality Feature
- echten fehlenden Core-Baustein

Core nur ändern, wenn die Fähigkeit wirklich generisch für mehrere Branchen notwendig ist.

---

# 69. Langfristiges Ziel

Die Plattform soll sich entwickeln von:

```text
Pilot 1 – Olive Oil
```

zu:

```text
Food Traceability Platform

Olive Oil
Dairy
Meat
Seafood
Produce
Wine
Honey
Grain
Processed Food
...
```

ohne für jede Kategorie eine neue Anwendung zu bauen.

Der technische Kern bleibt stabil.

---

# 70. Prioritäten

Wenn Geschwindigkeit, Eleganz und Zukunftsfähigkeit kollidieren:

1. Datenintegrität
2. Traceability-Korrektheit
3. Security / Tenant Isolation
4. Verständliche Architektur
5. Testbarkeit
6. Erweiterbarkeit
7. Performance
8. UI-Polish

Traceability darf niemals zugunsten eines schnelleren UI-Hacks unzuverlässig werden.

---

# 71. Abschlussregel für Codex

Vor jeder größeren Änderung:

```text
Preserve the generic food-platform architecture.
Do not optimize the system for only the current pilot.
Do not introduce complexity without a current or clearly planned use case.
Keep traceability deterministic, auditable, testable, and tenant-safe.
```


# Swagger – verbindliche Regel

Swagger ist fester Bestandteil jeder Backend-API.

Verwende:

```text
Swashbuckle.AspNetCore
```

Pflicht:

- OpenAPI-Dokument erzeugen
- Swagger UI in Development aktivieren
- JWT/Bearer Authentication in Swagger UI konfigurieren
- Request-/Response-DTOs dokumentieren
- HTTP-Statuscodes dokumentieren
- `ProblemDetails` und Validation Responses darstellen
- neue Endpunkte müssen sofort in Swagger erscheinen
- `/swagger/v1/swagger.json` muss gültig bleiben

Eine API-Aufgabe ist nicht DONE, solange der Endpoint nicht korrekt in Swagger dokumentiert und testbar ist.


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
