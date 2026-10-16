# Changelog

Todos los cambios relevantes de este proyecto se registran en este archivo.

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/).

## [Unreleased]

### Added

- [FEAT-001a] Acceso a la aplicación con una única contraseña:
  - creación inicial;
  - ingreso con bloqueo de 5 intentos durante 5 minutos;
  - sesión por cookie de 60 minutos con renovación;
  - cambio de contraseña;
  - blanqueo por línea de comandos con auditoría.
- [FEAT-001a] Configuración del negocio: alícuota de IVA, condición fiscal, tope de identificación del receptor y múltiplo de redondeo comercial.
- [FEAT-001a] Interfaz web en React con la marca de Óptica Sistema: pantallas de ingreso, creación de contraseña y Configuración.
- [FEAT-001a] Persistencia en SQLite con migraciones versionadas. Los importes se guardan como enteros escalados (ADR-001).
- [FEAT-001a] Seguridad:
  - contraseña con hash PBKDF2;
  - antiforgery;
  - encabezados de seguridad y CSP;
  - el servidor escucha solo en loopback;
  - los logs no incluyen secretos.
- [FEAT-001a] Documentación de operación: instalación como servicio de Windows, blanqueo de contraseña, pruebas de punta a punta y medición de cobertura.
- [FEAT-001a] Pruebas de punta a punta con Playwright en Chrome y Edge, y umbral de cobertura del 80 % en la web.
