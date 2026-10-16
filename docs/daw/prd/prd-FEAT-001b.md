# PRD FEAT-001b: Catálogo de artículos y cálculo del precio de venta

| Campo | Valor |
|-------|-------|
| Ticket | FEAT-001b |
| Tracker | none |
| Fecha | 2026-10-16 |
| PRD loops | 1 |
| PRD padre | `docs/daw/prd/prd-FEAT-001.md` |
| Fuente | `docs/daw/prd/PRD.md` (PRD de producto); la referencia `[PRD.md X]` indica el requisito de origen |

## Contexto y Problema
Hoy los precios se buscan en un folleto que envían los proveedores. Esos precios son de costo (con IVA incluido) y no tienen el margen de ganancia, así que el precio de venta se calcula a mano, con riesgo de errores de producto y de precio.

Este sub-ticket entrega el catálogo de artículos con el precio de venta calculado automáticamente a partir del costo, el margen de utilidad y los parámetros de FEAT-001a, y la actualización masiva de costos desde una planilla Excel.

## Objetivos
- Que el precio de venta de cada artículo lo calcule el sistema, sin cálculos a mano.
- Actualizar los costos de muchos artículos de una vez cuando el proveedor cambia la lista.
- Que el catálogo esté listo para que FEAT-001c lo use en las líneas de presupuesto.

## Requerimientos Funcionales
- FR-01: El sistema debe permitir cargar artículos con los campos Código, Código en el proveedor, Descripción, Precio de Costo (precio final del folleto del proveedor, con IVA incluido) y Margen de Utilidad (%). [PRD.md RF-20]
- FR-02: El sistema debe asignar el Código del artículo automáticamente (autonumérico) al grabarlo por primera vez. [PRD.md RF-20]
- FR-03: El sistema debe calcular el Precio de Venta de cada artículo (precio final al consumidor, con IVA incluido) según FR-04 o FR-05, sin que la operadora lo cargue. [PRD.md RF-69]
- FR-04: Cuando la condición fiscal configurada sea Responsable Inscripto, el sistema debe calcular el precio de venta en tres pasos: (a) Costo sin IVA = Precio de Costo / (1 + alícuota de IVA / 100); (b) Precio de Venta sin IVA = Costo sin IVA × (1 + Margen de Utilidad / 100); (c) Precio de Venta = Precio de Venta sin IVA × (1 + alícuota de IVA / 100). Los pasos (a) y (b) se calculan sin redondear; solo se redondea el resultado final según FR-06. [PRD.md RF-21]
- FR-05: Cuando la condición fiscal configurada sea Monotributo, el sistema debe calcular el precio de venta como Precio de Costo × (1 + Margen de Utilidad / 100), sin descontar IVA del costo. [PRD.md RF-84]
- FR-06: El sistema debe redondear el precio de venta calculado hacia arriba, hasta el múltiplo de redondeo comercial configurado, de modo que nunca quede por debajo del valor calculado. [PRD.md RF-56]
- FR-07: El sistema debe guardar el precio de venta ya redondeado según FR-06. [PRD.md RF-58]
- FR-08: El sistema debe mostrar el Precio de Venta del artículo como un campo de solo lectura. [PRD.md RF-70]
- FR-09: El sistema debe recalcular y guardar el precio de venta de un artículo cuando se modifique su precio de costo o su margen de utilidad. [PRD.md RF-45]
- FR-10: El sistema debe recalcular y guardar el precio de venta de todos los artículos del catálogo, activos e inactivos, cuando se grabe un cambio en la alícuota de IVA, la condición fiscal o el múltiplo de redondeo comercial. [PRD.md RF-86]
- FR-11: El sistema debe permitir cargar una planilla Excel `.xlsx` para actualizar los precios de costo, tomando la primera hoja, con los encabezados `Codigo proveedor` y `Precio costo` en la fila 1, en ese orden, y los datos desde la fila 2. Los encabezados se comparan sin distinguir mayúsculas ni acentos. [PRD.md RF-22, RF-23; formato decidido en DEFINE, 2026-10-16]
- FR-12: El sistema debe actualizar el precio de costo de cada artículo cuyo Código en el proveedor coincida con una fila de la planilla, recalculando su precio de venta según FR-04 o FR-05. [PRD.md RF-47]
- FR-13: El sistema debe comparar el Código en el proveedor sin distinguir mayúsculas de minúsculas e ignorando los espacios en blanco al principio y al final. [PRD.md RF-94]
- FR-14: El sistema debe rechazar completa, sin actualizar ningún artículo, una planilla que no respete el formato de FR-11 (archivo que no es `.xlsx`, columnas faltantes, columnas de más o encabezados distintos). [PRD.md RF-78]
- FR-15: El sistema debe informar a la operadora que el formato de la planilla no es el esperado cuando la rechace según FR-14. [PRD.md RF-79]
- FR-16: El sistema debe considerar inválido un Precio costo vacío, no numérico o cuyo valor absoluto supere $100.000.000, y aceptar los valores negativos y el cero dentro de ese rango. [PRD.md RF-80; tope decidido en PLAN, 2026-10-16]
- FR-17: El sistema debe mostrar en pantalla la lista de las filas que no pudo actualizar y la razón de cada caso ("código inexistente" o "precio inválido"). [PRD.md RF-24]
- FR-18: El sistema debe listar, junto con el resultado de FR-17, las filas que actualizó con un precio de costo negativo o igual a cero, con la indicación "actualizado con precio negativo o cero". [PRD.md RF-93]
- FR-19: El sistema debe permitir desactivar un artículo, conservando todos sus datos. (Decisión de DEFINE, 2026-10-16: no se eliminan artículos.)
- FR-20: El sistema debe permitir reactivar un artículo desactivado. (Decisión de DEFINE, 2026-10-16.)
- FR-21: El sistema debe actualizar por planilla también los artículos inactivos, sin reactivarlos. (Decisión de DEFINE, 2026-10-16.)
- FR-22: El sistema debe permitir buscar artículos en la pantalla del catálogo por código, por código en el proveedor o por descripción, por coincidencia parcial y sin distinguir mayúsculas ni acentos, mostrando activos e inactivos con su estado. (Supuesto: ver la sección Supuestos.)
- FR-23: El sistema debe rechazar, sin grabar el artículo, un Margen de Utilidad menor que 0% o mayor que 1.000%, mostrando el mensaje "El margen debe estar entre 0 y 1.000" junto al campo. (Decisión de PLAN, 2026-10-16: reemplaza el supuesto S-03 "sin tope".)
- FR-24: El sistema debe rechazar, sin grabar el artículo, un Precio de Costo cuyo valor absoluto supere $100.000.000, mostrando el mensaje "El precio de costo no puede superar $100.000.000" junto al campo. (Decisión de PLAN, 2026-10-16.)

## Requerimientos No Funcionales
- NFR-01: El sistema debe soportar un catálogo de hasta 10.000 artículos y procesar una planilla de hasta 10.000 filas en menos de 120 segundos. [PRD.md RNF-12]
- NFR-02: El sistema debe calcular y redondear los importes con aritmética decimal exacta de al menos 2 decimales, sin errores de representación de punto flotante.
- NFR-03: Todos los criterios de aceptación de este PRD deben cumplirse en Chrome y en Edge, en sus 2 últimas versiones estables (la vigente y la inmediata anterior). [PRD.md RNF-03]

## Criterios de Aceptación
- AC-01 (FR-01, FR-02, FR-03, FR-07): WHEN se graba un artículo nuevo indicando Código en el proveedor, Descripción, Precio de Costo y Margen de Utilidad, THE sistema SHALL asignarle automáticamente el Código autonumérico y guardarlo con su Precio de Venta calculado. [PRD.md AC-30]
- AC-02 (FR-04, FR-06): WHILE la condición fiscal es Responsable Inscripto, la alícuota de IVA es 21 y el múltiplo de redondeo comercial es 0,01, WHEN se graba un artículo con precio de costo $1.000 y margen de utilidad 50%, THE sistema SHALL guardar un precio de venta de $1.500,00. [PRD.md AC-13]
- AC-03 (FR-04): WHILE la condición fiscal es Responsable Inscripto, la alícuota de IVA es 21 y el múltiplo de redondeo comercial es 0,01, WHEN se graba un artículo con precio de costo $1.210 y margen de utilidad 50%, THE sistema SHALL guardar un precio de venta de $1.815,00, que resulta de un costo sin IVA de $1.000,00 y un precio de venta sin IVA de $1.500,00. [PRD.md AC-36]
- AC-04 (FR-05): WHILE la condición fiscal es Monotributo y el múltiplo de redondeo comercial es 0,01, WHEN se graba un artículo con precio de costo $1.210 y margen de utilidad 50%, THE sistema SHALL guardar un precio de venta de $1.815,00. [PRD.md AC-83]
- AC-05 (FR-06, FR-07): WHILE el múltiplo de redondeo comercial es $50, WHEN se graba un artículo cuyo precio de venta calculado es $1.815,37, THE sistema SHALL guardar un precio de venta de $1.850,00. [PRD.md AC-38]
- AC-06 (FR-06, FR-07): WHILE el múltiplo de redondeo comercial es $0,01, WHEN se graba un artículo cuyo precio de venta calculado es $1.666,656, THE sistema SHALL guardar un precio de venta de $1.666,66. [PRD.md AC-39]
- AC-07 (FR-08): WHEN se abre un artículo grabado para editarlo, THE sistema SHALL mostrar el campo Precio de Venta sin permitir modificar su valor. [PRD.md AC-81]
- AC-08 (FR-09): WHILE la alícuota de IVA es 21 y el múltiplo de redondeo comercial es $0,01, WHEN a un artículo con precio de costo $1.210, margen 50% y precio de venta $1.815,00 se le cambia el margen a 60% y se graba, THE sistema SHALL guardar un precio de venta de $1.936,00. [PRD.md AC-70]
- AC-09 (FR-09): WHILE la alícuota de IVA es 21 y el múltiplo de redondeo comercial es $0,01, WHEN a un artículo con precio de costo $1.210, margen 50% y precio de venta $1.815,00 se le cambia a mano el precio de costo a $2.420 y se graba, THE sistema SHALL guardar un precio de venta de $3.630,00. [PRD.md AC-71]
- AC-10 (FR-04): WHILE la condición fiscal es Responsable Inscripto y el múltiplo de redondeo comercial es $0,01, WHEN se cambia la alícuota de IVA de 21 a 10,5 y luego se graba un artículo con precio de costo $1.105 y margen de utilidad 50%, THE sistema SHALL guardar un precio de venta de $1.657,50. [PRD.md AC-72]
- AC-11 (FR-10): WHEN un artículo tiene precio de costo $1.210, margen 50% y precio de venta guardado $1.815,00 con el múltiplo en $0,01, y en Configuración se cambia el múltiplo a $50 y se graba, THE sistema SHALL guardar para ese artículo un precio de venta de $1.850,00 sin que se edite el artículo. [PRD.md AC-74, AC-85]
- AC-12 (FR-11, FR-12): WHILE la alícuota de IVA es 21, el múltiplo de redondeo comercial es $0,01 y existe un artículo con Código en el proveedor "ABC-1", precio de costo $1.210 y margen 50%, WHEN se carga una planilla con el formato de FR-11 que contiene la fila "ABC-1" con precio $2.420, THE sistema SHALL guardar el precio de costo del artículo en $2.420,00 y su precio de venta en $3.630,00. [PRD.md AC-14]
- AC-13 (FR-11): WHEN se carga una planilla cuyos encabezados son "CÓDIGO PROVEEDOR" y "precio costo", THE sistema SHALL aceptar el formato y procesar sus filas.
- AC-14 (FR-13): WHEN existe un artículo con Código en el proveedor "ABC-1" y se carga una planilla con la fila "abc-1" y precio $2.420, THE sistema SHALL guardar el precio de costo de ese artículo en $2.420,00 y no listarlo como no actualizado. [PRD.md AC-91]
- AC-15 (FR-13): WHEN existe un artículo con Código en el proveedor "ABC-1" y se carga una planilla con la fila " ABC-1 " (con espacios al principio y al final) y precio $2.420, THE sistema SHALL guardar el precio de costo de ese artículo en $2.420,00 y no listarlo como no actualizado. [PRD.md AC-92]
- AC-16 (FR-14, FR-15): IF se carga una planilla con una tercera columna además de las dos de FR-11, THEN THE sistema SHALL no actualizar ningún artículo y mostrar el mensaje "El formato de la planilla no es el esperado: la primera fila debe tener solo las columnas Codigo proveedor y Precio costo". [PRD.md AC-76]
- AC-17 (FR-14, FR-15): IF se carga un archivo `.csv`, o una planilla `.xlsx` cuyos encabezados son "Codigo" y "Precio", THEN THE sistema SHALL no actualizar ningún artículo y mostrar el mismo mensaje de formato no esperado.
- AC-18 (FR-17): IF la planilla contiene una fila con un código de proveedor que no existe en el catálogo, THEN THE sistema SHALL actualizar las filas válidas y mostrar esa fila en la lista de no actualizadas con la razón "código inexistente". [PRD.md AC-15]
- AC-19 (FR-16, FR-17): IF la planilla contiene una fila con precio vacío, otra con el precio "abc" y otras filas válidas, THEN THE sistema SHALL actualizar las filas válidas y listar las dos filas mencionadas como no actualizadas con la razón "precio inválido". [PRD.md AC-75]
- AC-20 (FR-16, FR-18): IF la planilla contiene una fila con precio -100 y otra con precio 0 para artículos existentes, THEN THE sistema SHALL actualizar ambos artículos y listarlos con la indicación "actualizado con precio negativo o cero". [PRD.md AC-90]
- AC-21 (FR-19): WHEN se desactiva un artículo, THE sistema SHALL mostrarlo como inactivo en el catálogo y conservar sin cambios su código, descripción, costo, margen y precio de venta.
- AC-22 (FR-20): WHEN se reactiva un artículo inactivo, THE sistema SHALL mostrarlo como activo en el catálogo.
- AC-23 (FR-21): WHEN se carga una planilla con el código de proveedor de un artículo inactivo y precio $2.420, THE sistema SHALL guardar su precio de costo en $2.420,00, recalcular su precio de venta y mantenerlo inactivo.
- AC-24 (FR-22): WHEN en la pantalla del catálogo se busca el texto "lente" y existen los artículos "Lente orgánico" (activo) y "LENTE policarbonato" (inactivo), THE sistema SHALL mostrar los dos, indicando el estado de cada uno.
- AC-25 (NFR-01): WHEN se procesa una planilla de 10.000 filas sobre un catálogo de 10.000 artículos, THE sistema SHALL actualizar los precios de costo y recalcular los de venta en menos de 120 segundos. [PRD.md AC-62]
- AC-26 (NFR-02): WHILE el múltiplo de redondeo comercial es $0,01, WHEN se graba un artículo cuyo precio de venta calculado es exactamente $1.666,66, THE sistema SHALL guardar $1.666,66 y no $1.666,67.
- AC-27 (NFR-03): WHEN se ejecutan los criterios de aceptación de este PRD en Chrome o en Edge, en su versión estable vigente o en la inmediata anterior, THE sistema SHALL cumplirlos todos. [PRD.md AC-56]
- AC-28 (FR-23): WHEN se graba un artículo con margen de utilidad 1.000, THE sistema SHALL guardarlo con ese margen.
- AC-29 (FR-23): IF se intenta grabar un artículo con margen de utilidad 1.000,01, THEN THE sistema SHALL no grabarlo y mostrar "El margen debe estar entre 0 y 1.000" junto al campo Margen de Utilidad.
- AC-30 (FR-23): IF se intenta grabar un artículo con margen de utilidad -0,01, THEN THE sistema SHALL no grabarlo y mostrar "El margen debe estar entre 0 y 1.000" junto al campo Margen de Utilidad.
- AC-31 (FR-24): WHEN se graba un artículo con precio de costo $100.000.000, THE sistema SHALL guardarlo con ese precio de costo.
- AC-32 (FR-24): IF se intenta grabar un artículo con precio de costo $100.000.000,01, THEN THE sistema SHALL no grabarlo y mostrar "El precio de costo no puede superar $100.000.000" junto al campo Precio de Costo.
- AC-33 (FR-16, FR-17): IF la planilla contiene una fila con precio 100.000.001 para un artículo existente y otras filas válidas, THEN THE sistema SHALL actualizar las filas válidas, no modificar ese artículo y listar la fila como no actualizada con la razón "precio inválido".

## Fuera de Alcance
- Eliminación física de artículos: solo se desactivan (FR-19).
- Alta de artículos nuevos desde la planilla: la planilla solo actualiza costos de artículos existentes; los códigos que no existen se informan como no actualizados.
- Control de stock: el sistema no lleva existencias.
- Historial de cambios de precios.
- Más de una alícuota de IVA en el catálogo.
- Uso de los artículos en presupuestos: lo implementa FEAT-001c.

## Supuestos
Puntos que el PRD de producto no define y que se fijan acá. Requieren la confirmación de la usuaria al aprobar este PRD:
- S-01 (FR-22): La pantalla del catálogo tiene una búsqueda propia, necesaria para encontrar el artículo que se quiere editar, desactivar o reactivar.
- S-02: El Código en el proveedor no es único: si dos artículos lo comparten, la planilla actualiza a los dos (FR-12 dice "cada artículo cuyo código coincida").
- S-03: (Reemplazado en PLAN, 2026-10-16, por FR-23 y FR-24.) El margen mínimo es 0%: no se admite un margen negativo, ni en el alta ni en la edición. El precio de costo negativo (hasta -$100.000.000) solo llega por planilla, según FR-16; al cargarlo a mano rige el mismo rango de FR-24.

## Riesgos y Mitigaciones
- Riesgo: Un precio de costo negativo o cero importado por planilla produce un precio de venta negativo o cero. Aceptar esos valores fue una decisión explícita del responsable del proyecto (PRD.md, Riesgos). Mitigación: el sistema lista esas filas para que la operadora las revise (FR-18), y FEAT-001c impide usar en un presupuesto un precio unitario negativo.
- Riesgo: Errores de redondeo por usar punto flotante en importes. Mitigación: NFR-02 y AC-26.
- Riesgo: El recálculo masivo de FR-10 sobre 10.000 artículos deja el catálogo a medio actualizar si falla. Mitigación: se define en PLAN (transacción única del recálculo).
- Riesgo: La IA que genere el código interprete mal las fórmulas. Mitigación: criterios con valores numéricos concretos (AC-02 a AC-11).

## Dependencias
- FEAT-001a: acceso con contraseña y parámetros de configuración (alícuota de IVA, condición fiscal, múltiplo de redondeo comercial). FR-04, FR-05, FR-06 y FR-10 los leen.
- Una librería para leer archivos `.xlsx` en .NET (se elige en PLAN).
- Stack del proyecto (AGENTS.md): ASP.NET Core Web API sobre .NET 10, EF Core 10, SQLite, React 19 con TypeScript y Vite.
- Consumidor: FEAT-001c toma de este catálogo el precio de venta guardado y excluye los artículos inactivos de su búsqueda.
