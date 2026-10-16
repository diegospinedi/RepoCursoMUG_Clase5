# ADR-002: Lector de planillas .xlsx con DocumentFormat.OpenXml

| Field | Value |
|-------|-------|
| Date | 2026-10-16 |
| Ticket | FEAT-001b |
| Status | Accepted |

## Context

FEAT-001b actualiza precios de costo a partir de una planilla `.xlsx` del proveedor (FR-11 a FR-18 de `prd-FEAT-001b.md`). El archivo cruza una frontera de confianza: puede traer un ZIP bomb, XML con DTD o macros, y contiene costos confidenciales (`threat-FEAT-001b.md`, R-01 a R-04). .NET no lee `.xlsx` sin una librería, así que hay que sumar una dependencia nueva.

## Options considered

### Option 1: DocumentFormat.OpenXml
- **Pros:** es el SDK oficial de Microsoft, con licencia MIT y sin dependencias de terceros. Permite lectura secuencial (SAX, `OpenXmlReader`) con memoria acotada. Prohíbe DTD y no evalúa fórmulas.
- **Cons:** la API es de bajo nivel, así que hay que resolver a mano los strings compartidos y las referencias de celda.

### Option 2: ClosedXML
- **Pros:** la API es cómoda y requiere menos código.
- **Cons:** carga la hoja entera en memoria (DOM), así que un archivo malicioso pega más fuerte. Suma dependencias de terceros, con más superficie de cadena de suministro. Además depende de OpenXml igual.

### Option 3: ExcelDataReader
- **Pros:** lectura secuencial y liviana, con licencia MIT.
- **Cons:** la mantiene la comunidad. Incluye soporte de `.xls` binario, que no se necesita y agrega superficie. No expone el tipo de la celda con la precisión que pide FR-16 (numérico frente a texto).

## Decision

Se elige la opción 1, decidida por el usuario el 2026-10-16. Es la de menor superficie de cadena de suministro y la única que combina lectura secuencial con control fino del tipo de celda. Ese control hace falta para tratar como "precio inválido" el texto, los valores con más de 2 decimales y las fórmulas sin valor guardado.

## Consequences

- **Dónde se usa:** solo en `src/Optica.Infrastructure/Planillas/`, detrás del puerto `ILectorPlanillaCostos` de Application. Ni Domain ni Api la referencian.
- **Versión:** exacta en `Optica.Infrastructure.csproj`, con los lockfiles actualizados y auditados con `dotnet list package --vulnerable`.
- **Validación previa del ZIP:** antes de abrir el paquete corre `InspectorZip`, porque OpenXml por sí solo no limita la descompresión.
- **Archivos de prueba:** las planillas de test se generan en memoria con el mismo SDK, así que no se commitean binarios.
- **Mantenimiento:** el código de lectura es más largo que con ClosedXML y queda bajo nuestra responsabilidad.
