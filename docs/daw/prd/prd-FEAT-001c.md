# PRD FEAT-001c: Presupuestos con líneas, cálculos y estados

| Campo | Valor |
|-------|-------|
| Ticket | FEAT-001c |
| Tracker | none |
| Fecha | 2026-10-16 |
| PRD loops | 0 |
| PRD padre | `docs/daw/prd/prd-FEAT-001.md` |
| Fuente | `docs/daw/prd/PRD.md` (PRD de producto); la referencia `[PRD.md X]` indica el requisito de origen |

## Contexto y Problema
Hoy los presupuestos se escriben a mano, copiando productos y precios de un folleto. Se cometen errores de producto, de precio y de cálculo, y no hay un registro centralizado: si el cliente pierde el presupuesto, no se puede recuperar.

Este sub-ticket entrega la carga de presupuestos con los datos del cliente y líneas de artículos tomados del catálogo (FEAT-001b), con los cálculos de descuento, precio final y total hechos por el sistema, la numeración automática y los estados Borrador y Final.

## Objetivos
- Que cada presupuesto quede grabado en un único lugar, con número propio.
- Eliminar los errores de transcripción y de cálculo tomando artículos y precios del catálogo.
- Que un presupuesto en estado Final no se pueda alterar.

## Requerimientos Funcionales
- FR-01: El sistema debe permitir grabar un presupuesto nuevo. [PRD.md RF-01]
- FR-02: El sistema debe permitir cargar en el presupuesto los datos del cliente: Apellido, Nombre, DNI, Domicilio, Email y Nro. de Teléfono. [PRD.md RF-05]
- FR-03: El sistema debe guardar los datos del cliente dentro del propio presupuesto. [PRD.md RF-38]
- FR-04: El sistema debe exigir Apellido, Nombre y DNI del cliente para grabar un presupuesto. [PRD.md RF-61]
- FR-05: El sistema debe permitir grabar un presupuesto sin Domicilio, Email ni Nro. de Teléfono del cliente. [PRD.md RF-74]
- FR-06: El sistema debe asignar a cada presupuesto, al grabarlo por primera vez, un número igual al último número asignado más 1. [PRD.md RF-06]
- FR-07: El sistema debe grabar en estado Borrador todo presupuesto que se graba por primera vez. [PRD.md RF-04]
- FR-08: El sistema debe permitir pasar un presupuesto de estado Borrador a estado Final. [PRD.md RF-04]
- FR-09: El sistema debe permitir modificar un presupuesto únicamente mientras se encuentre en estado Borrador. [PRD.md RF-07]
- FR-10: El sistema no debe permitir modificar un presupuesto en estado Final, por pantalla ni por API. [PRD.md RF-08]
- FR-11: El sistema no debe permitir devolver un presupuesto en estado Final al estado Borrador. [PRD.md RF-67]
- FR-12: El sistema debe permitir cargar productos por línea en el presupuesto. [PRD.md RF-10]
- FR-13: El sistema debe permitir buscar artículos del catálogo por código o por descripción al cargar una línea. [PRD.md RF-11]
- FR-14: El sistema debe excluir los artículos inactivos de la búsqueda de FR-13. (Decisión de DEFINE, 2026-10-16.)
- FR-15: El sistema debe permitir seleccionar para la línea un artículo encontrado según FR-13. [PRD.md RF-68]
- FR-16: El sistema debe completar automáticamente el código, la descripción y el precio unitario de la línea al seleccionar un artículo. [PRD.md RF-39]
- FR-17: El sistema debe usar como precio unitario de la línea el precio de venta guardado en el catálogo, sin volver a calcularlo. [PRD.md RF-73]
- FR-18: El sistema debe permitir ajustar la cantidad y el porcentaje de descuento de la línea después de seleccionar el artículo. [PRD.md RF-40]
- FR-19: El sistema debe registrar en cada línea los campos Código de Artículo, Descripción, Precio Unitario, Cantidad, Porcentaje de Descuento, Precio con Descuento y Precio Final. [PRD.md RF-12]
- FR-20: El sistema debe calcular el precio con descuento de cada línea como Precio Unitario × (1 − Porcentaje de Descuento / 100). [PRD.md RF-13]
- FR-21: El sistema debe mostrar el precio con descuento en todas las líneas, incluidas las de descuento cero, en cuyo caso es igual al precio unitario. [PRD.md RF-41]
- FR-22: El sistema debe calcular el precio final de cada línea como el precio con descuento por la cantidad. [PRD.md RF-14]
- FR-23: El sistema debe redondear a nivel de línea, primero el precio con descuento y luego el precio final de la línea. [PRD.md RF-44]
- FR-24: El sistema debe redondear los importes a 2 decimales con redondeo estándar (mitad hacia arriba). [PRD.md RF-19]
- FR-25: El sistema debe calcular el total del presupuesto como la suma de los precios finales, ya redondeados, de todas las líneas. [PRD.md RF-15]
- FR-26: El sistema no debe admitir cantidades negativas ni iguales a cero. [PRD.md RF-16]
- FR-27: El sistema debe admitir únicamente cantidades enteras. [PRD.md RF-60]
- FR-28: El sistema no debe admitir líneas con precio unitario negativo. [PRD.md RF-17]
- FR-29: El sistema no debe admitir porcentajes de descuento menores a 0 ni mayores a 100. [PRD.md RF-59]
- FR-30: El sistema debe expresar con IVA incluido (precio final al consumidor) todos los precios que muestra en el presupuesto. [PRD.md RF-18]
- FR-31: El sistema no debe discriminar IVA en el presupuesto. [PRD.md RF-42]
- FR-32: El sistema no debe modificar los precios de las líneas de los presupuestos ya grabados cuando cambie el precio de venta de un artículo del catálogo. [PRD.md RF-87]
- FR-33: El sistema debe exigir al menos 1 línea para grabar un presupuesto. (Supuesto: ver la sección Supuestos.)
- FR-34: El sistema debe registrar como fecha del presupuesto la fecha en que se graba por primera vez. (Supuesto: ver la sección Supuestos.)
- FR-35: El sistema debe mostrar la lista de presupuestos grabados, del número más alto al más bajo, en páginas de 50, con número, fecha, apellido, nombre, estado y total, y permitir abrir cualquiera de ellos. (Supuesto: ver la sección Supuestos.)

## Requerimientos No Funcionales
- NFR-01: El sistema debe soportar 2 sesiones simultáneas grabando presupuestos sin que ninguna reciba errores de bloqueo de la base de datos. [PRD.md RNF-13]
- NFR-02: El sistema debe calcular y redondear los importes con aritmética decimal exacta de al menos 2 decimales, sin errores de representación de punto flotante.
- NFR-03: Todos los criterios de aceptación de este PRD deben cumplirse en Chrome y en Edge, en sus 2 últimas versiones estables (la vigente y la inmediata anterior). [PRD.md RNF-03]

## Criterios de Aceptación
- AC-01 (FR-01, FR-02, FR-03): WHEN se carga un presupuesto nuevo con Apellido, Nombre, DNI, Domicilio, Email y Nro. de Teléfono y al menos 1 línea con un artículo, y se presiona Grabar, THE sistema SHALL grabarlo, mostrar un mensaje que confirma que se grabó y mostrar esos seis datos al volver a abrirlo. [PRD.md AC-01, AC-23]
- AC-02 (FR-07): WHEN se graba por primera vez un presupuesto nuevo con Apellido, Nombre, DNI y al menos 1 línea, THE sistema SHALL dejarlo en estado Borrador y el mensaje de confirmación SHALL indicar ese estado. [PRD.md AC-04]
- AC-03 (FR-08): WHEN en un presupuesto en estado Borrador se cambia el estado a Final y se graba, THE sistema SHALL grabarlo en estado Final. [PRD.md AC-05]
- AC-04 (FR-06): WHEN el último presupuesto grabado tiene el número 154 y se graba un presupuesto nuevo por primera vez, THE sistema SHALL asignarle el número 155. [PRD.md AC-06]
- AC-05 (FR-09): WHEN en un presupuesto en estado Borrador se modifica el Domicilio y se presiona Grabar, THE sistema SHALL grabar el cambio, mostrar un mensaje de confirmación y mostrar el nuevo Domicilio al volver a abrirlo. [PRD.md AC-07]
- AC-06 (FR-12): WHEN en un presupuesto en estado Borrador se agrega una línea y se graba, THE sistema SHALL guardar la línea y mostrarla en la grilla de líneas. [PRD.md AC-24]
- AC-07 (FR-13, FR-15, FR-16): WHEN al cargar una línea se busca un artículo por código o por descripción y se lo selecciona, THE sistema SHALL completar automáticamente el código, la descripción y el precio unitario con el precio de venta del catálogo. [PRD.md AC-10]
- AC-08 (FR-14): IF al cargar una línea se busca la descripción de un artículo inactivo, THEN THE sistema SHALL no mostrarlo entre los resultados.
- AC-09 (FR-17): WHEN se selecciona en una línea un artículo cuyo precio de venta guardado es $1.850,00, THE sistema SHALL poner como precio unitario $1.850,00, tomado tal cual del catálogo. [PRD.md AC-40]
- AC-10 (FR-18): WHEN en una línea con un artículo ya seleccionado se modifican la cantidad y el porcentaje de descuento, THE sistema SHALL tomar los nuevos valores y recalcular el precio con descuento y el precio final de la línea. [PRD.md AC-46]
- AC-11 (FR-19): WHEN se abre un presupuesto con una línea cargada, THE sistema SHALL mostrar para esa línea los siete campos: Código de Artículo, Descripción, Precio Unitario, Cantidad, Porcentaje de Descuento, Precio con Descuento y Precio Final. [PRD.md AC-25]
- AC-12 (FR-20): WHEN en una línea con precio unitario $1.000 se carga un descuento de 10%, THE sistema SHALL mostrar un precio con descuento de $900,00. [PRD.md AC-35]
- AC-13 (FR-20, FR-21): WHEN en una línea con precio unitario $1.000 se carga un descuento de 0%, THE sistema SHALL mostrar un precio con descuento de $1.000,00. [PRD.md AC-26]
- AC-14 (FR-22): WHEN en una línea con precio con descuento $900 se carga cantidad 3, THE sistema SHALL mostrar un precio final de línea de $2.700,00. [PRD.md AC-27]
- AC-15 (FR-23, FR-24): WHEN en una línea con precio unitario $6,67 se carga un descuento de 50% (precio con descuento sin redondear $3,335) y cantidad 3, THE sistema SHALL mostrar un precio con descuento de $3,34 y un precio final de línea de $10,02. [PRD.md AC-29]
- AC-16 (FR-25): WHEN a un presupuesto con la línea de AC-15 (precio final $10,02) se le agrega una segunda línea con precio final $2.700,00, THE sistema SHALL mostrar un total de $2.710,02. [PRD.md AC-11]
- AC-17 (FR-26, FR-27): IF en una línea se ingresa una cantidad de -1, de 0 o de 2,5, THEN THE sistema SHALL no aceptar el valor y mostrar junto al campo el mensaje "La cantidad debe ser un número entero mayor a 0". [PRD.md AC-12, AC-43]
- AC-18 (FR-28): IF se selecciona para una línea un artículo cuyo precio de venta es negativo, THEN THE sistema SHALL no aceptar la línea y mostrar junto al campo el mensaje "El precio unitario no puede ser negativo". [PRD.md AC-12]
- AC-19 (FR-29): IF en una línea se ingresa un descuento de -5 o de 101, THEN THE sistema SHALL no aceptar el valor y mostrar junto al campo el mensaje "El descuento debe estar entre 0 y 100". [PRD.md AC-42]
- AC-20 (FR-30, FR-31): WHEN se abre en pantalla un presupuesto con líneas cargadas, THE sistema SHALL mostrar las líneas y el total sin discriminar IVA, con importes de precio final al consumidor con IVA incluido. [PRD.md AC-28]
- AC-21 (FR-04): IF se presiona Grabar en un presupuesto nuevo con Apellido y Nombre cargados y sin DNI, THEN THE sistema SHALL no grabarlo y mostrar junto al campo DNI el mensaje "Ingresá el DNI del cliente". [PRD.md AC-34]
- AC-22 (FR-04): IF se presiona Grabar en un presupuesto sin Apellido o sin Nombre, THEN THE sistema SHALL no grabarlo y mostrar junto al campo faltante el mensaje "Ingresá el apellido del cliente" o "Ingresá el nombre del cliente", según corresponda. [PRD.md AC-44]
- AC-23 (FR-05): WHEN se presiona Grabar en un presupuesto con Apellido, Nombre y DNI cargados y sin Domicilio, Email ni Nro. de Teléfono, THE sistema SHALL grabarlo sin reclamar esos tres campos. [PRD.md AC-45]
- AC-24 (FR-10): IF se intenta modificar cualquier dato o línea de un presupuesto en estado Final, por pantalla o llamando directamente a la API, THEN THE sistema SHALL rechazar el cambio y el presupuesto SHALL quedar sin modificaciones. [PRD.md AC-66]
- AC-25 (FR-11): IF se intenta cambiar a Borrador el estado de un presupuesto en estado Final, por pantalla o por API, THEN THE sistema SHALL rechazar el cambio y el presupuesto SHALL seguir en estado Final. [PRD.md AC-67]
- AC-26 (FR-32): WHEN un presupuesto grabado tiene una línea con precio unitario $1.815,00 y el precio de venta del artículo en el catálogo pasa a $1.850,00, THE sistema SHALL conservar en la línea del presupuesto el precio unitario $1.815,00. [PRD.md AC-86]
- AC-27 (FR-33): IF se presiona Grabar en un presupuesto sin ninguna línea, THEN THE sistema SHALL no grabarlo y mostrar junto a la grilla de líneas el mensaje "Agregá al menos una línea".
- AC-28 (FR-34): WHEN un presupuesto grabado por primera vez el 10/03/2026 se modifica el 12/03/2026, THE sistema SHALL mantener como fecha del presupuesto el 10/03/2026.
- AC-29 (FR-35): WHEN se abre la lista de presupuestos y hay 60 grabados, THE sistema SHALL mostrar en la primera página los 50 de número más alto, ordenados de mayor a menor, con número, fecha, apellido, nombre, estado y total, y permitir abrir cualquiera de ellos.
- AC-30 (NFR-01, FR-06): WHEN dos sesiones abiertas al mismo tiempo graban un presupuesto nuevo simultáneamente, THE sistema SHALL grabar los dos con números distintos y consecutivos, sin que ninguna sesión reciba un error de bloqueo de la base. [PRD.md AC-63]
- AC-31 (NFR-02): WHEN en una línea con precio unitario $0,10 se carga un descuento de 0% y cantidad 3, THE sistema SHALL mostrar un precio final de línea de $0,30 exacto.
- AC-32 (NFR-03): WHEN se ejecutan los criterios de aceptación de este PRD en Chrome o en Edge, en su versión estable vigente o en la inmediata anterior, THE sistema SHALL cumplirlos todos. [PRD.md AC-56]

## Fuera de Alcance
- Búsqueda de presupuestos por fecha y datos del cliente, y generación y descarga del PDF: los implementa FEAT-001d.
- Facturación electrónica a partir de un presupuesto Final: ticket de facturación ARCA.
- Eliminación de presupuestos, incluidos los que están en estado Borrador.
- Módulo o ABM de clientes: los datos del cliente se cargan en cada presupuesto y no se guardan como entidad aparte.
- Modificar a mano el precio unitario de una línea: se toma del catálogo.
- Registro de cobros, señas y medios de pago; control de stock.

## Supuestos
Puntos que el PRD de producto no define y que se fijan acá. Requieren la confirmación de la usuaria al aprobar este PRD:
- S-01 (FR-33): Un presupuesto necesita al menos 1 línea para grabarse, incluso en Borrador. Los criterios del PRD de producto siempre parten de "al menos 1 línea".
- S-02 (FR-34): La fecha del presupuesto es la de su primera grabación y no cambia al modificarlo ni al pasarlo a Final. Es la fecha que usa la búsqueda de FEAT-001d.
- S-03 (FR-35): Sin una lista de presupuestos no hay forma de volver a abrir uno hasta que llegue la búsqueda de FEAT-001d. La lista se pagina de a 50.
- S-04 (AC-22, AC-27): Los textos de los mensajes de validación son los indicados en cada criterio.

## Riesgos y Mitigaciones
- Riesgo: Los presupuestos guardan datos personales (DNI, domicilio, teléfono, email) alcanzados por la Ley 25.326. Mitigación: este sub-ticket no se entrega sin FEAT-001a (acceso con contraseña) y debe entregarse junto con FEAT-001e o después (backup).
- Riesgo: Dos sesiones que graban a la vez obtienen el mismo número o reciben un error de bloqueo de SQLite. Mitigación: NFR-01 y AC-30; la estrategia de asignación del número se define en PLAN.
- Riesgo: Un artículo con precio de venta negativo (importado por planilla en FEAT-001b) llega a un presupuesto. Mitigación: FR-28 y AC-18.
- Riesgo: Un presupuesto Final se altera por una vía no prevista. Mitigación: FR-10 y FR-11 se verifican también llamando directamente a la API (AC-24 y AC-25).

## Dependencias
- FEAT-001a: acceso con contraseña y mensajes de validación.
- FEAT-001b: catálogo con precio de venta guardado y estado activo o inactivo (FR-13, FR-14 y FR-17).
- FEAT-001e: backup de la base, que debe estar en funcionamiento antes de cargar presupuestos con datos reales.
- Stack del proyecto (AGENTS.md): ASP.NET Core Web API sobre .NET 10, EF Core 10, SQLite, React 19 con TypeScript y Vite.
