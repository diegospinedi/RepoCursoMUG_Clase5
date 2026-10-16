# PRD FEAT-001e: Backup automático de la base de datos

| Campo | Valor |
|-------|-------|
| Ticket | FEAT-001e |
| Tracker | none |
| Fecha | 2026-10-16 |
| PRD loops | 1 |
| PRD padre | `docs/daw/prd/prd-FEAT-001.md` |
| Fuente | `docs/daw/prd/PRD.md` (PRD de producto); la referencia `[PRD.md X]` indica el requisito de origen |

## Contexto y Problema
La base de datos es un archivo SQLite en la PC del local. Si el disco falla, se pierden el catálogo, los presupuestos y los datos personales de los clientes. Además, SQLite trabaja en modo WAL: copiar el archivo de la base con una copia común puede dejar afuera los cambios recientes, que todavía están en `optica.db-wal`.

Este sub-ticket entrega el backup automático diario en una ubicación externa, la retención de copias, el aviso cuando un backup no se ejecutó y el procedimiento documentado de restauración. Debe estar funcionando antes de que FEAT-001c empiece a guardar datos reales de clientes.

## Objetivos
- Que una falla del disco no provoque pérdida de datos de más de 1 día.
- Que las operadoras se enteren el mismo día siguiente si el backup dejó de correr.
- Poder restaurar la base en menos de 4 horas.

## Requerimientos Funcionales
- FR-01: El sistema debe realizar un backup automático de la base de datos todos los días a las 20:00, con la PC del local encendida. [PRD.md RNF-02]
- FR-02: El sistema debe guardar cada copia en la ubicación de backup definida en el archivo de configuración del servidor, ubicada fuera del disco principal (pendrive, carpeta sincronizada en la nube u otra). [PRD.md RNF-02; decisión de DEFINE, 2026-10-16]
- FR-03: El sistema debe generar cada copia con `VACUUM INTO` o con la API de backup de SQLite, de modo que incluya los cambios que todavía están en el archivo WAL, y nunca con una copia de archivo común. [AGENTS.md, "What NOT to do"]
- FR-04: El sistema debe nombrar cada copia con la fecha del día en que se generó.
- FR-05: El sistema debe conservar las 7 copias diarias más recientes y 4 copias mensuales, y eliminar las demás. [PRD.md RNF-05]
- FR-06: El sistema debe registrar el resultado de cada ejecución del backup (fecha, hora, éxito o error con su motivo).
- FR-07: El sistema debe avisar a la operadora, al ingresar, cuando el backup del día anterior no se haya ejecutado con éxito, indicando la fecha del último backup disponible. [PRD.md RF-77]
- FR-08: El sistema no debe mostrar ni permitir editar la ubicación de backup desde la interfaz. (Decisión de DEFINE, 2026-10-16.)
- FR-09: El sistema debe contar con un procedimiento documentado para restaurar la base a partir de una copia de backup en una PC con el sistema instalado. [PRD.md RNF-06]

## Requerimientos No Funcionales
- NFR-01: La restauración de la base a partir de una copia de backup debe completarse en menos de 4 horas, siguiendo el procedimiento de FR-09, en una PC con el sistema instalado según ese procedimiento. [PRD.md RNF-06]
- NFR-02: El backup debe ejecutarse aunque haya 0 sesiones abiertas en el navegador: el servidor de la aplicación corre como servicio de Windows. (Decisión de DEFINE, 2026-10-16.)
- NFR-03: Todos los criterios de aceptación de este PRD deben cumplirse en Chrome y en Edge, en sus 2 últimas versiones estables (la vigente y la inmediata anterior). [PRD.md RNF-03]

## Criterios de Aceptación
- AC-01 (FR-01, FR-02, FR-04): WHEN se cumplen las 20:00 con la PC del local encendida y el sistema en operación, THE sistema SHALL iniciar el backup y, al terminar, la ubicación de backup configurada SHALL contener una copia de la base con la fecha del día en su nombre. [PRD.md AC-52]
- AC-02 (NFR-02): WHILE no hay ninguna sesión abierta en el navegador, WHEN se cumplen las 20:00, THE sistema SHALL ejecutar el backup igual que en AC-01.
- AC-03 (FR-03): WHEN se graba un artículo 1 minuto antes de las 20:00 y se ejecuta el backup, THE copia generada SHALL contener ese artículo al abrirla en un sistema restaurado.
- AC-04 (FR-05): WHEN el sistema opera desde hace más de 4 meses y se revisa la ubicación de backup, THE ubicación SHALL contener exactamente las 7 copias diarias más recientes y 4 copias mensuales. [PRD.md AC-53]
- AC-05 (FR-06, FR-07): IF el backup del día anterior no se ejecutó, THEN THE sistema SHALL mostrar al ingresar un aviso que indica esa situación y la fecha del último backup disponible. [PRD.md AC-55]
- AC-06 (FR-06, FR-07): IF a las 20:00 la ubicación de backup no está disponible (por ejemplo, el pendrive está desconectado), THEN THE sistema SHALL registrar la ejecución como fallida con su motivo, seguir operando normalmente y mostrar el aviso de AC-05 en el ingreso del día siguiente.
- AC-07 (FR-07): WHEN el backup del día anterior se ejecutó con éxito, THE sistema SHALL no mostrar el aviso al ingresar.
- AC-08 (FR-08): WHEN se recorren todas las pantallas del sistema con sesión iniciada, THE sistema SHALL no mostrar ni permitir editar la ubicación de backup en ninguna.
- AC-09 (FR-09, NFR-01): WHEN se restaura la base a partir de una copia de backup siguiendo el procedimiento documentado, en una PC con el sistema instalado según ese procedimiento, THE sistema SHALL permitir abrir un artículo que figura en la copia en menos de 4 horas desde el inicio de la restauración. [PRD.md AC-54, con artículo en lugar de presupuesto porque este sub-ticket se entrega antes que FEAT-001c]
- AC-10 (NFR-03): WHEN se ejecutan los criterios de aceptación de este PRD en Chrome o en Edge, en su versión estable vigente o en la inmediata anterior, THE sistema SHALL cumplirlos todos. [PRD.md AC-56]

## Fuera de Alcance
- Backup a pedido desde la interfaz: solo corre el backup automático diario.
- Restauración desde la interfaz: se hace con el procedimiento documentado.
- Configuración de la ubicación de backup desde la interfaz: la define el técnico en el archivo de configuración del servidor.
- Backup en un servicio en la nube administrado por el sistema: si se usa la nube, es mediante una carpeta sincronizada que el sistema ve como una carpeta local.
- Cifrado de las copias de backup: las copias se guardan sin cifrar (riesgo aceptado, ver Riesgos y Mitigaciones).

## Supuestos
Puntos que el PRD de producto no define y que se fijan acá. Requieren la confirmación de la usuaria al aprobar este PRD:
- S-01 (FR-05): La copia mensual que se conserva es la primera copia exitosa de cada mes.
- S-02 (FR-07): Si la PC estuvo apagada a las 20:00, el backup de ese día no se ejecuta y se avisa al ingresar al día siguiente. El sistema no recupera backups perdidos.

## Riesgos y Mitigaciones
- Riesgo: Copiar la base con una copia de archivo común pierde los cambios que están en el WAL. Mitigación: FR-03 y AC-03.
- Riesgo: El backup deja de correr sin que nadie lo note (pendrive desconectado, servicio detenido). Mitigación: registro de cada ejecución (FR-06) y aviso al ingresar (FR-07, AC-05 y AC-06).
- Riesgo: Las copias contienen datos personales de clientes (Ley 25.326) y quedan en un pendrive o en una carpeta sincronizada en la nube, fuera de la protección de la contraseña del sistema. Si el pendrive se pierde o la cuenta de la nube se ve comprometida, los datos quedan expuestos. **Riesgo aceptado:** las copias no se cifran.
  - Quién lo aceptó: el responsable del proyecto, en DEFINE, el 2026-10-16.
  - Justificación: se prioriza la simplicidad de la restauración (sin una clave que se pueda perder) en una instalación de una sola PC; la protección es el resguardo físico del pendrive y el control de acceso a la cuenta de la nube donde está la carpeta sincronizada.
  - Condiciones de revisión: se revisa si se pierde o se roba un medio con copias, si la ubicación de backup pasa a un medio compartido con terceros, si el sistema empieza a guardar datos más sensibles que los actuales (por ejemplo, datos de salud visual del cliente) o, como máximo, en la revisión del ticket de facturación ARCA.
  - Resguardos mínimos (sin costo de implementación): el pendrive se guarda bajo llave fuera del horario de atención, y la cuenta de la nube usa una contraseña propia con verificación en dos pasos. Ambos quedan documentados en el procedimiento de FR-09.
- Riesgo: La PC está apagada a las 20:00. Mitigación: el aviso del día siguiente (S-02).

## Dependencias
- FEAT-001a: acceso con contraseña y pantalla de ingreso, donde se muestra el aviso de FR-07.
- Servidor de la aplicación instalado como servicio de Windows (NFR-02).
- Ubicación de backup externa al disco principal (pendrive o carpeta sincronizada), definida por el técnico en el archivo de configuración del servidor.
- SQLite en modo WAL, con `VACUUM INTO` o API de backup disponibles.
- Stack del proyecto (AGENTS.md): ASP.NET Core Web API sobre .NET 10, EF Core 10, SQLite, React 19 con TypeScript y Vite.
