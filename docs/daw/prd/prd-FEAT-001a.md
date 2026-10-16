# PRD FEAT-001a: Acceso con contraseña y configuración del negocio

| Campo | Valor |
|-------|-------|
| Ticket | FEAT-001a |
| Tracker | none |
| Fecha | 2026-10-16 |
| PRD loops | 0 |
| PRD padre | `docs/daw/prd/prd-FEAT-001.md` |
| Fuente | `docs/daw/prd/PRD.md` (PRD de producto); la referencia `[PRD.md X]` indica el requisito de origen |

## Contexto y Problema
Óptica Sistema va a guardar en el sistema datos personales de clientes (DNI, domicilio, teléfono, email), alcanzados por la Ley 25.326 de Protección de Datos Personales. Antes de que el sistema guarde cualquier dato, el acceso tiene que estar protegido con contraseña. Además, el cálculo de precios del catálogo (FEAT-001b) depende de parámetros del negocio (alícuota de IVA, condición fiscal, múltiplo de redondeo comercial) que hoy no existen en ningún lado.

Este sub-ticket entrega la base sobre la que se construyen los demás: el acceso con una única contraseña compartida por las dos dueñas (no hay módulo de usuarios), la pantalla de configuración de los parámetros del negocio y la identidad visual de la óptica.

## Objetivos
- Impedir que alguien sin la contraseña vea o modifique datos del sistema, por pantalla o por API.
- Que las operadoras puedan administrar los parámetros del negocio sin intervención técnica.
- Que todas las pantallas muestren la marca de la óptica.

## Requerimientos Funcionales
- FR-01: El sistema debe exigir una sesión iniciada con la contraseña de acceso para mostrar cualquier pantalla y para responder cualquier endpoint de la API, salvo la pantalla y el endpoint de ingreso y los de creación inicial de la contraseña. [PRD.md RNF-04]
- FR-02: El sistema debe mostrar la pantalla de creación de la contraseña, en lugar de la de ingreso, mientras no exista una contraseña definida. (Decisión de DEFINE, 2026-10-16.)
- FR-03: El sistema no debe mostrar la pantalla de creación inicial ni aceptar su endpoint cuando ya exista una contraseña definida.
- FR-04: El sistema no debe aceptar una contraseña de menos de 8 caracteres, ni al crearla ni al cambiarla. [PRD.md RNF-04]
- FR-05: El sistema debe permitir cambiar la contraseña desde la pantalla de Configuración, exigiendo la contraseña actual. (Decisión de DEFINE, 2026-10-16.)
- FR-06: El sistema debe proveer un procedimiento documentado de blanqueo de la contraseña que se ejecuta localmente en la PC donde corre el servidor, sin ninguna pantalla ni endpoint web, y que deja el sistema sin contraseña definida para que se vuelva a crear según FR-02. (Decisión de DEFINE, 2026-10-16: no hay administrador que pueda resolver un olvido.)
- FR-07: El sistema debe permitir administrar, desde la pantalla de Configuración, la alícuota de IVA, la condición fiscal, el tope de identificación del receptor y el múltiplo de redondeo comercial. [PRD.md RF-63]
- FR-08: El sistema debe guardar la alícuota de IVA, expresada en porcentaje (por ejemplo, 21), como un único parámetro aplicable a todo el catálogo. [PRD.md RF-46]
- FR-09: El sistema debe guardar la condición fiscal de la óptica como un parámetro con dos valores posibles: Responsable Inscripto o Monotributo. [PRD.md RF-48]
- FR-10: El sistema debe guardar el tope de identificación del receptor como un parámetro configurable, para que lo use la facturación electrónica (ticket posterior). [PRD.md RF-49]
- FR-11: El sistema debe guardar el múltiplo de redondeo comercial como un parámetro configurable. [PRD.md RF-57]
- FR-12: El sistema no debe admitir un múltiplo de redondeo comercial menor a 0,01. [PRD.md RF-72]
- FR-13: El sistema no debe admitir una alícuota de IVA menor a 0 ni mayor a 100. [PRD.md RF-21: la alícuota se expresa en porcentaje de 0 a 100]
- FR-14: El sistema no debe admitir un tope de identificación del receptor negativo. (Supuesto: ver la sección Supuestos.)
- FR-15: El sistema debe iniciar, en una base nueva, con la alícuota de IVA en 21, la condición fiscal en Responsable Inscripto, el múltiplo de redondeo comercial en 0,01 y el tope de identificación sin valor cargado. (Supuesto: ver la sección Supuestos.)
- FR-16: El sistema debe mostrar en todas sus pantallas el logo y la paleta de colores de la óptica definidos en `Marca/branding.json` (logo `Marca/logo.png`; color primario #0903A0 sobre fondo blanco #FFFFFF). [PRD.md RF-34]
- FR-17: El sistema debe indicar, en todo error de validación, el campo afectado y la acción correctiva que debe tomar la operadora. [PRD.md RF-35]

## Requerimientos No Funcionales
- NFR-01: El sistema debe guardar la contraseña únicamente como hash con sal aleatoria de al menos 16 bytes (PBKDF2, bcrypt o equivalente), nunca en texto plano. [PRD.md RNF-14]
- NFR-02: El sistema debe bloquear el ingreso durante 5 minutos tras 5 intentos fallidos consecutivos; el bloqueo es temporal y se libera solo. [PRD.md RNF-08]
- NFR-03: El sistema debe cerrar la sesión tras 60 minutos de inactividad y volver a exigir la contraseña. [PRD.md RNF-09]
- NFR-04: Todos los criterios de aceptación de este PRD deben cumplirse en Chrome y en Edge, en sus 2 últimas versiones estables (la vigente y la inmediata anterior). [PRD.md RNF-03]

## Criterios de Aceptación
- AC-01 (FR-01): IF no hay una sesión iniciada y se abre cualquier pantalla o se llama a cualquier endpoint de la API distinto de los de ingreso y de creación inicial, THEN THE sistema SHALL redirigir a la pantalla de ingreso o responder 401, sin devolver datos de clientes, presupuestos, artículos ni configuración. [PRD.md AC-79]
- AC-02 (FR-02): WHEN se abre el sistema y no existe una contraseña definida, THE sistema SHALL mostrar la pantalla de creación de la contraseña.
- AC-03 (FR-03): IF ya existe una contraseña definida y se pide la pantalla o el endpoint de creación inicial, THEN THE sistema SHALL rechazar el pedido y la contraseña existente SHALL seguir siendo la única aceptada en el ingreso.
- AC-04 (FR-04, FR-17): IF se ingresa una contraseña de 7 caracteres al crearla o al cambiarla, THEN THE sistema SHALL rechazarla y mostrar junto al campo el mensaje "La contraseña debe tener al menos 8 caracteres". [PRD.md AC-57]
- AC-05 (FR-05): WHEN en Configuración se ingresan la contraseña actual correcta y una contraseña nueva de 8 o más caracteres y se graba, THE sistema SHALL aceptar en el ingreso siguiente la contraseña nueva y rechazar la anterior.
- AC-06 (FR-05, FR-17): IF al cambiar la contraseña se ingresa una contraseña actual incorrecta, THEN THE sistema SHALL rechazar el cambio, mostrar junto al campo el mensaje "La contraseña actual no es correcta" y seguir aceptando solo la contraseña anterior.
- AC-07 (FR-06): WHEN se ejecuta en la PC del servidor el procedimiento documentado de blanqueo, THE sistema SHALL quedar sin contraseña definida y mostrar la pantalla de creación en el ingreso siguiente.
- AC-08 (FR-06): IF se recorren todas las pantallas y todos los endpoints de la API buscando una forma de blanquear la contraseña, THEN THE sistema SHALL no ofrecer ninguna que lo permita.
- AC-09 (NFR-02): IF se ingresa 5 veces seguidas una contraseña incorrecta, THEN THE sistema SHALL bloquear el ingreso durante 5 minutos, rechazando incluso la contraseña correcta en ese lapso, y volver a admitir el ingreso al cumplirse los 5 minutos sin intervención de nadie. [PRD.md AC-58]
- AC-10 (NFR-03): WHEN transcurren 60 minutos sin actividad en una sesión iniciada, THE sistema SHALL cerrar la sesión y exigir nuevamente la contraseña. [PRD.md AC-59]
- AC-11 (NFR-01): WHEN se inspeccionan la base de datos y los archivos de configuración del sistema con una contraseña ya definida, THE contenido inspeccionado SHALL no incluir la contraseña en texto plano. [PRD.md AC-80]
- AC-12 (FR-07, FR-08, FR-09, FR-10, FR-11): WHEN en la pantalla de Configuración se cambian la alícuota de IVA a 10,5, la condición fiscal a Monotributo, el tope de identificación a 100000 y el múltiplo de redondeo comercial a 50, y se graba, THE sistema SHALL guardar los cuatro valores y mostrarlos al volver a abrir la pantalla. [PRD.md AC-49, adaptado: la facturación es un ticket posterior]
- AC-13 (FR-12, FR-17): IF se ingresa un múltiplo de redondeo comercial de 0,001, THEN THE sistema SHALL no aceptarlo y mostrar junto al campo el mensaje "El valor mínimo es 0,01". [PRD.md AC-82]
- AC-14 (FR-13, FR-17): IF se ingresa una alícuota de IVA de -1 o de 101, THEN THE sistema SHALL no aceptarla y mostrar junto al campo el mensaje "La alícuota de IVA debe estar entre 0 y 100".
- AC-15 (FR-14, FR-17): IF se ingresa un tope de identificación de -1, THEN THE sistema SHALL no aceptarlo y mostrar junto al campo el mensaje "El tope no puede ser negativo".
- AC-16 (FR-15): WHEN se abre la pantalla de Configuración por primera vez sobre una base nueva, THE sistema SHALL mostrar la alícuota de IVA en 21, la condición fiscal en Responsable Inscripto, el múltiplo de redondeo comercial en 0,01 y el tope de identificación vacío.
- AC-17 (FR-16): WHEN se abre cualquier pantalla del sistema, incluidas las de ingreso y de creación de la contraseña, THE sistema SHALL mostrar el logo `Marca/logo.png` y aplicar el color primario #0903A0 sobre fondo #FFFFFF, tal como figuran en `Marca/branding.json`. [PRD.md AC-33, parte pantallas]
- AC-18 (NFR-04): WHEN se ejecutan los criterios de aceptación de este PRD en Chrome o en Edge, en su versión estable vigente o en la inmediata anterior, THE sistema SHALL cumplirlos todos. [PRD.md AC-56]

## Fuera de Alcance
- Módulo de usuarios: altas, bajas, roles y contraseñas individuales. Hay una sola contraseña compartida.
- Recuperación de la contraseña por email o por cualquier vía web. El olvido se resuelve con el procedimiento local de FR-06.
- Uso del tope de identificación en la emisión de facturas: lo implementa el ticket de facturación ARCA.
- Configuración del certificado digital y del punto de venta de ARCA (PRD.md RF-64): pertenece al ticket de facturación.
- Acceso al sistema desde internet.

## Supuestos
Puntos que el PRD de producto no define y que se fijan acá para poder escribir criterios binarios. Requieren la confirmación de la usuaria al aprobar este PRD:
- S-01 (FR-15): Valores iniciales de la configuración: IVA 21, condición fiscal Responsable Inscripto, múltiplo 0,01 y tope sin cargar. Si la óptica es Monotributo, se cambia desde Configuración antes de cargar el catálogo.
- S-02 (FR-14): El tope de identificación no puede ser negativo.
- S-03 (AC-04, AC-06, AC-14, AC-15): Los textos de los mensajes de validación son los indicados en cada criterio.

## Riesgos y Mitigaciones
- Riesgo: Hasta que se crea la contraseña, quien llegue primero a la pantalla inicial puede definirla. Mitigación: la contraseña se crea durante la instalación y el sistema solo es accesible desde la red local del negocio.
- Riesgo: Ataque de fuerza bruta sobre la contraseña desde la red local. Mitigación: bloqueo temporal tras 5 intentos fallidos (NFR-02) y longitud mínima de 8 caracteres (FR-04).
- Riesgo: Olvido de la contraseña sin administrador que la blanquee. Mitigación: procedimiento local documentado (FR-06).
- Riesgo: Exposición de datos personales (Ley 25.326) si un endpoint queda sin protección. Mitigación: FR-01 exige sesión en todos los endpoints salvo ingreso y creación inicial, y AC-01 lo verifica.

## Dependencias
- Archivos de marca `Marca/branding.json` y `Marca/logo.png` (FR-16).
- Stack del proyecto (AGENTS.md): ASP.NET Core Web API sobre .NET 10, EF Core 10, SQLite, React 19 con TypeScript y Vite.
- PC del local donde corre el servidor, accesible solo desde esa PC o la red local (FR-06 y riesgos).
- Consumidores de esta configuración: FEAT-001b (alícuota, condición fiscal y múltiplo de redondeo) y el ticket de facturación ARCA (tope de identificación).
