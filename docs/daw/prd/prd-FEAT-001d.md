# PRD FEAT-001d: Búsqueda y PDF de presupuestos

| Campo | Valor |
|-------|-------|
| Ticket | FEAT-001d |
| Tracker | none |
| Fecha | 2026-10-16 |
| PRD loops | 0 |
| PRD padre | `docs/daw/prd/prd-FEAT-001.md` |
| Fuente | `docs/daw/prd/PRD.md` (PRD de producto); la referencia `[PRD.md X]` indica el requisito de origen |

## Contexto y Problema
Con los presupuestos grabados (FEAT-001c), las operadoras necesitan encontrarlos cuando el cliente vuelve o los pide de nuevo, y entregarlos en PDF para mandarlos por email o WhatsApp de forma manual.

Este sub-ticket entrega la búsqueda de presupuestos por fecha y por datos del cliente, y la generación y descarga del PDF de los presupuestos en estado Final.

## Objetivos
- Encontrar cualquier presupuesto por fecha, apellido, nombre o DNI en menos de 2 segundos.
- Entregar al cliente un PDF con la marca de la óptica, generado solo para presupuestos cerrados.

## Requerimientos Funcionales
- FR-01: El sistema debe permitir buscar presupuestos por fecha y por datos del cliente (apellido, nombre, DNI). [PRD.md RF-03]
- FR-02: El sistema debe combinar con "Y" los filtros cargados en la búsqueda: solo muestra los presupuestos que cumplen todos. [PRD.md RF-81]
- FR-03: El sistema debe buscar el apellido y el nombre por coincidencia parcial (el dato guardado contiene el texto ingresado), sin distinguir mayúsculas ni acentos. [PRD.md RF-82]
- FR-04: El sistema debe filtrar por fecha mediante un rango Desde/Hasta, con ambos límites inclusive, admitiendo que se cargue uno solo de los dos. [PRD.md RF-83]
- FR-05: El sistema debe buscar el DNI por coincidencia parcial, ignorando puntos y guiones tanto en el dato guardado como en el texto ingresado. [PRD.md RF-88]
- FR-06: El sistema debe generar el presupuesto en formato PDF. [PRD.md RF-02]
- FR-07: El sistema no debe permitir generar ni descargar el PDF de un presupuesto en estado Borrador, por pantalla ni por API. [PRD.md RF-09]
- FR-08: El sistema debe permitir descargar en la PC el PDF generado, para que la operadora lo adjunte manualmente en un email o un mensaje de WhatsApp. [PRD.md RF-36]
- FR-09: El sistema debe incluir en el PDF la leyenda "Precios finales, IVA incluido". [PRD.md RF-37]
- FR-10: El sistema debe incluir en el PDF el logo y la paleta de colores de la óptica. [PRD.md RF-55, parte presupuesto]
- FR-11: El sistema debe incluir en el PDF el número y la fecha del presupuesto, los datos cargados del cliente, las líneas con sus siete campos y el total. (Supuesto: ver la sección Supuestos.)

## Requerimientos No Funcionales
- NFR-01: La búsqueda de presupuestos debe responder en menos de 2 segundos en el percentil 95, con hasta 10.000 presupuestos almacenados, medido desde que la operadora presiona Buscar hasta que la grilla muestra los resultados en pantalla. [PRD.md RNF-01]
- NFR-02: Todos los criterios de aceptación de este PRD deben cumplirse en Chrome y en Edge, en sus 2 últimas versiones estables (la vigente y la inmediata anterior). [PRD.md RNF-03]

## Criterios de Aceptación
- AC-01 (FR-01, FR-03): WHEN existen presupuestos de clientes con apellido "González" y "Gómez" y se busca por apellido el texto "ONZALEZ", THE sistema SHALL mostrar en la grilla solo el presupuesto de "González". [PRD.md AC-03]
- AC-02 (FR-03): WHEN existe un presupuesto de una clienta de nombre "María" y se busca por nombre el texto "mari", THE sistema SHALL mostrar ese presupuesto en la grilla.
- AC-03 (FR-01, FR-02, FR-04): WHEN existen dos presupuestos de clientes con apellido "González", uno del 10/03/2026 y otro del 20/03/2026, y se busca por apellido "González" con fecha Desde 15/03/2026 y sin fecha Hasta, THE sistema SHALL mostrar en la grilla solo el presupuesto del 20/03/2026. [PRD.md AC-68]
- AC-04 (FR-04): WHEN existen presupuestos del 09/03/2026, 10/03/2026, 20/03/2026 y 21/03/2026 y se busca con fecha Desde 10/03/2026 y Hasta 20/03/2026, THE sistema SHALL mostrar en la grilla solo los del 10/03/2026 y del 20/03/2026. [PRD.md AC-69]
- AC-05 (FR-01, FR-05): WHEN existe un presupuesto de un cliente con DNI 23.456.789 y se busca por DNI el texto "3456", THE sistema SHALL mostrar ese presupuesto en la grilla. [PRD.md AC-87, parte presupuestos]
- AC-06 (FR-05): WHEN existe un presupuesto de un cliente con DNI guardado como 23456789 y se busca por DNI el texto "23.456", THE sistema SHALL mostrar ese presupuesto en la grilla.
- AC-07 (FR-06, FR-08, FR-10): WHEN en un presupuesto en estado Final se presiona Descargar PDF, THE sistema SHALL generar un PDF con el logo y los colores de la óptica y descargarlo en la PC. [PRD.md AC-02, AC-09]
- AC-08 (FR-07): IF se abre un presupuesto en estado Borrador, o se pide su PDF llamando directamente a la API, THEN THE sistema SHALL mostrar deshabilitado el botón Descargar PDF y la API SHALL rechazar el pedido sin generar el PDF. [PRD.md AC-08]
- AC-09 (FR-09): WHEN se descarga el PDF de un presupuesto en estado Final, THE documento SHALL mostrar la leyenda "Precios finales, IVA incluido". [PRD.md AC-37]
- AC-10 (FR-10): WHEN se genera el PDF de un presupuesto, THE documento SHALL mostrar el logo `Marca/logo.png` y aplicar el color primario #0903A0 sobre fondo #FFFFFF, tal como figuran en `Marca/branding.json`. [PRD.md AC-33, parte PDF de presupuesto]
- AC-11 (FR-11): WHEN se descarga el PDF de un presupuesto en estado Final con 2 líneas, THE documento SHALL mostrar el número y la fecha del presupuesto, los datos cargados del cliente, las 2 líneas con sus siete campos y el total, con los mismos importes que la pantalla.
- AC-12 (NFR-01): WHEN sobre una base con 10.000 presupuestos se ejecutan 20 búsquedas por apellido, THE sistema SHALL mostrar la grilla de resultados en menos de 2 segundos, contados desde la pulsación de Buscar, en al menos 19 de ellas. [PRD.md AC-51]
- AC-13 (NFR-02): WHEN se ejecutan los criterios de aceptación de este PRD en Chrome o en Edge, en su versión estable vigente o en la inmediata anterior, THE sistema SHALL cumplirlos todos. [PRD.md AC-56]

## Fuera de Alcance
- Búsqueda de facturas y PDF de facturas: ticket de facturación ARCA.
- Envío automático del PDF por email o WhatsApp: la operadora lo descarga y lo adjunta a mano.
- Impresión directa desde el sistema: se imprime desde el PDF descargado.
- PDF de presupuestos en estado Borrador.

## Supuestos
Puntos que el PRD de producto no define y que se fijan acá. Requieren la confirmación de la usuaria al aprobar este PRD:
- S-01 (FR-11): El PRD de producto no enumera el contenido del PDF del presupuesto. Se toma el mismo contenido que se ve en pantalla.
- S-02 (FR-04): La fecha que se filtra es la fecha del presupuesto definida en FEAT-001c (la de su primera grabación).

## Riesgos y Mitigaciones
- Riesgo: El PDF descargado contiene datos personales del cliente y sale del sistema por email o WhatsApp. Mitigación: se genera solo a pedido de la operadora con sesión iniciada (FEAT-001a) y solo para presupuestos Final; el envío es manual y queda bajo su responsabilidad.
- Riesgo: La búsqueda con coincidencia parcial sin acentos sobre 10.000 presupuestos supera los 2 segundos. Mitigación: NFR-01 y AC-12; la estrategia de búsqueda se define en PLAN.

## Dependencias
- FEAT-001c: presupuestos grabados con número, fecha, datos del cliente, líneas, total y estado.
- FEAT-001a: acceso con contraseña.
- Archivos de marca `Marca/branding.json` y `Marca/logo.png` (FR-10).
- Una librería para generar PDF en .NET (se elige en PLAN).
- Stack del proyecto (AGENTS.md): ASP.NET Core Web API sobre .NET 10, EF Core 10, SQLite, React 19 con TypeScript y Vite.
