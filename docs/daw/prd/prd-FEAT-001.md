# PRD padre: Presupuestos online con catálogo de artículos

| Métrica | Valor |
|--------|-------|
| Ticket | FEAT-001 |
| Fecha | 2026-10-16 |
| Estado | Dividido |

## Sub-tickets

| Sub-ticket | Título | PRD | Dependencias | Estado |
|---|---|---|---|---|
| FEAT-001a | Acceso con contraseña y configuración del negocio | prd-FEAT-001a.md | ninguna | active |
| FEAT-001b | Catálogo de artículos y cálculo del precio de venta | prd-FEAT-001b.md | depende de a | pending |
| FEAT-001e | Backup automático de la base de datos | prd-FEAT-001e.md | depende de a; se entrega antes o junto con c | pending |
| FEAT-001c | Presupuestos con líneas, cálculos y estados | prd-FEAT-001c.md | depende de a, b y e | pending |
| FEAT-001d | Búsqueda y PDF de presupuestos | prd-FEAT-001d.md | depende de c | pending |

## Orden sugerido de implementación
a → b → e → c → d

> **La columna Estado se mantiene, no es decorativa.** El cierre de RELEASE pasa el sub-ticket
> terminado a `done`, con el destino de su rama, y el siguiente a `active`.

## Contexto original
`docs/daw/prd/PRD.md` es el PRD del producto completo: presupuestos con catálogo de precios calculados y factura electrónica a consumidor final vía ARCA. En CLASSIFY, FEAT-001 se acotó a presupuestos, catálogo y configuración, y la facturación ARCA quedó para un ticket posterior. Aun así, el recorte sumaba unos 60 criterios de aceptación en 4 áreas, por lo que en DEFINE (2026-10-16) se dividió en cinco sub-tickets entregables por separado.

El acceso con contraseña va primero porque los presupuestos guardan datos personales de clientes alcanzados por la Ley 25.326. El backup se separó en su propio sub-ticket y debe estar funcionando antes de cargar presupuestos con datos reales.

Decisiones tomadas en DEFINE:
- Contraseña única: se crea en el primer ingreso, se cambia desde Configuración ingresando la actual y se blanquea con un procedimiento local documentado, sin vía web.
- Los artículos se desactivan y reactivan; no se eliminan.
- Planilla de costos: `.xlsx`, primera hoja, encabezados `Codigo proveedor` y `Precio costo`.
- Ubicación del backup: la define el técnico en el archivo de configuración del servidor; el servidor corre como servicio de Windows.

## Fuera de FEAT-001
Facturación electrónica ARCA y todo lo que depende de ella en `PRD.md`: RF-25 a RF-33, RF-43, RF-50 a RF-54, RF-62, RF-64 a RF-66, RF-71, RF-75, RF-76, RF-85, RF-89 a RF-92, RNF-10 y RNF-11, más la búsqueda de facturas.
