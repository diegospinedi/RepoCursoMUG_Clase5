# ADR-001: Importes y porcentajes en SQLite como enteros escalados ×10.000

| Campo | Valor |
|-------|-------|
| Fecha | 2026-10-16 |
| Ticket | FEAT-001a |
| Estado | Aceptado |

## Contexto
SQLite no tiene un tipo decimal exacto. El proveedor SQLite de EF Core 10 guarda `decimal` como TEXT y no traduce a SQL las comparaciones, `ORDER BY` ni los agregados sobre esas columnas. FEAT-001a guarda la alícuota de IVA, el tope de identificación y el múltiplo de redondeo; FEAT-001b (`prd-FEAT-001b.md`, NFR-02) y FEAT-001c (`prd-FEAT-001c.md`, NFR-02) exigen aritmética decimal exacta y van a comparar, ordenar y sumar precios sobre catálogos y presupuestos de hasta 10.000 registros.

## Opciones consideradas

### Opción 1: INTEGER escalado ×10.000 (4 decimales fijos)
- **Pros:** exacto; independiente de la cultura del sistema; SQL puede comparar, ordenar y sumar; rango de `long` sobrado (±9,2 × 10^14).
- **Cons:** requiere un conversor y disciplina para no leer la columna cruda; valores con más de 4 decimales no se pueden guardar.

### Opción 2: TEXT con formato invariante
- **Pros:** exacto; es lo que EF Core hace por defecto (con conversión invariante explícita).
- **Cons:** SQL no puede comparar ni sumar; filtros y totales se resuelven en memoria; ordenar como texto da resultados erróneos ("100" < "20").

### Opción 3: REAL (punto flotante)
- **Pros:** nativo de SQLite, sin conversión.
- **Cons:** errores de representación (0,1 × 3 ≠ 0,3); viola NFR-02 de FEAT-001b y FEAT-001c.

## Decisión
Opción 1, elegida por la usuaria el 2026-10-16. Es la única que es exacta y a la vez deja que la base compare, ordene y sume, lo que FEAT-001b y FEAT-001c van a necesitar.

## Consecuencias
- Un único `ValueConverter<decimal, long>` en `src/Optica.Infrastructure/Persistencia/` (escala 10.000) se aplica por convención a toda propiedad `decimal` del modelo. El dominio y la aplicación siguen usando `decimal`.
- El conversor rechaza (excepción) un valor con más de 4 decimales en lugar de truncarlo: el dominio redondea antes de persistir.
- Aplica desde FEAT-001a a `AlicuotaIva`, `TopeIdentificacion` y `MultiploRedondeo`; los sub-tickets siguientes lo heredan.
- Las consultas SQL crudas o los reportes que lean estas columnas deben dividir por 10.000.
- Test obligatorio de ida y vuelta (10,5; 0,01; 100000) y de rechazo de 0,00001.
