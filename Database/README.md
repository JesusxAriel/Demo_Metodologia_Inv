# Base de datos de SGAFU

## Instalación oficial desde cero

Para preparar una base de datos nueva, el único archivo que deben ejecutar los
desarrolladores es [`bdAutoGestionSereci.sql`](./bdAutoGestionSereci.sql).
Incluye las siete tablas actuales, relaciones, claves foráneas, índices,
restricciones, estados operativos, historial de asignaciones y datos de
demostración.

Requisitos previos:

- MySQL 8.0 o posterior.
- Un usuario con permisos para eliminar y crear la base de datos
  `bdAutoGestionSereci`.
- Un cliente MySQL que ejecute el archivo completo, respetando su orden.

**Advertencia:** el script contiene `DROP DATABASE IF EXISTS
bdAutoGestionSereci`. Elimina y recrea la base de datos, por lo que solo debe
usarse para una instalación nueva o cuando se haya confirmado que los datos
existentes pueden perderse. No lo ejecutes contra una base con información que
deba conservarse y no lo ejecutes desde la aplicación.

No hay scripts incrementales en esta entrega. Las tres migraciones históricas
fueron revisadas y sus cambios ya están incorporados en el script oficial:

- Los estados `Activa`, `Inactiva`, `Finalizando turno` y `En descanso`.
- La tabla `TurnosAsignacionesHistorial`, sus relaciones, restricciones e
  índices.

Por decisión de esta primera entrega, no se conserva compatibilidad con
instalaciones anteriores. No se debe buscar ni ejecutar una migración
secundaria junto con el script principal.
