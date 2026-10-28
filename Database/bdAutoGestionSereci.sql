-- ============================================================
-- Archivo: bdAutoGestionSereci.sql
-- Sistema: SGAFU - SERECI Cochabamba
-- Motor: MySQL 8.0+
--
-- SCRIPT UNIFICADO PARA INSTALACION DESDE CERO.
-- Incluye todas las tablas, relaciones, restricciones, indices,
-- estados operativos, historial de asignaciones y datos demo.
--
-- ADVERTENCIA:
-- Este script elimina y recrea bdAutoGestionSereci.
-- EJECUTAR SOLO si se desea reiniciar la base desde cero.
-- No ejecutar sobre una base con datos que se quieran conservar.
-- ============================================================

DROP DATABASE IF EXISTS bdAutoGestionSereci;

CREATE DATABASE bdAutoGestionSereci
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE bdAutoGestionSereci;

SET NAMES utf8mb4;

-- ============================================================
-- 1. TABLA: Tramites
-- ============================================================

CREATE TABLE Tramites (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Nombre VARCHAR(100) NOT NULL,
    Descripcion TEXT,
    TiempoEstimadoMinutos INT NOT NULL,
    NivelDemanda VARCHAR(20) NOT NULL DEFAULT 'Media',
    Estado VARCHAR(20) NOT NULL DEFAULT 'Activo',

    CONSTRAINT chk_tramites_tiempo
        CHECK (TiempoEstimadoMinutos > 0),

    CONSTRAINT chk_tramites_demanda
        CHECK (NivelDemanda IN ('Alta', 'Media', 'Baja')),

    CONSTRAINT chk_tramites_estado
        CHECK (Estado IN ('Activo', 'Inactivo')),

    CONSTRAINT uq_tramites_nombre
        UNIQUE (Nombre),

    INDEX idx_tramites_estado (Estado),
    INDEX idx_tramites_demanda (NivelDemanda)
) ENGINE = InnoDB;

-- ============================================================
-- 2. TABLA: PuntosAtencion
-- Incluye todos los estados operativos usados por la aplicacion.
-- ============================================================

CREATE TABLE PuntosAtencion (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Nombre VARCHAR(50) NOT NULL,
    CargaActualPorcentaje INT NOT NULL DEFAULT 0,
    ColaUsuarios INT NOT NULL DEFAULT 0,
    Estado VARCHAR(30) NOT NULL DEFAULT 'Activa',

    CONSTRAINT chk_puntos_carga
        CHECK (CargaActualPorcentaje BETWEEN 0 AND 100),

    CONSTRAINT chk_puntos_cola
        CHECK (ColaUsuarios >= 0),

    CONSTRAINT chk_puntos_estado
        CHECK (
            Estado IN (
                'Activa',
                'Inactiva',
                'Finalizando turno',
                'En descanso'
            )
        ),

    CONSTRAINT uq_puntos_nombre
        UNIQUE (Nombre),

    INDEX idx_puntos_estado_carga
        (Estado, CargaActualPorcentaje)
) ENGINE = InnoDB;

-- ============================================================
-- 3. TABLA: TramitePuntoAtencion
-- ============================================================

CREATE TABLE TramitePuntoAtencion (
    TramiteId INT NOT NULL,
    PuntoAtencionId INT NOT NULL,

    PRIMARY KEY (TramiteId, PuntoAtencionId),

    CONSTRAINT fk_tpa_tramite
        FOREIGN KEY (TramiteId)
        REFERENCES Tramites (Id)
        ON UPDATE CASCADE
        ON DELETE CASCADE,

    CONSTRAINT fk_tpa_punto
        FOREIGN KEY (PuntoAtencionId)
        REFERENCES PuntosAtencion (Id)
        ON UPDATE CASCADE
        ON DELETE CASCADE,

    INDEX idx_tpa_punto (PuntoAtencionId)
) ENGINE = InnoDB;

-- ============================================================
-- 4. TABLA: RegistrosDemanda
-- ============================================================

CREATE TABLE RegistrosDemanda (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    FranjaHoraria VARCHAR(30) NOT NULL,
    TramiteId INT NOT NULL,
    CantidadUsuarios INT NOT NULL,
    Fecha DATE NOT NULL,

    CONSTRAINT fk_demanda_tramite
        FOREIGN KEY (TramiteId)
        REFERENCES Tramites (Id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT chk_demanda_cantidad
        CHECK (CantidadUsuarios >= 0),

    CONSTRAINT uq_demanda_franja
        UNIQUE (Fecha, FranjaHoraria, TramiteId),

    INDEX idx_demanda_fecha_franja
        (Fecha, FranjaHoraria),

    INDEX idx_demanda_tramite_fecha
        (TramiteId, Fecha)
) ENGINE = InnoDB;

-- ============================================================
-- 5. TABLA: EscenariosSimulacion
-- ============================================================

CREATE TABLE EscenariosSimulacion (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Nombre VARCHAR(100) NOT NULL,
    CantidadUsuariosEstimados INT NOT NULL,
    PuntosActivos INT NOT NULL,
    HoraInicio TIME NOT NULL,
    HoraFin TIME NOT NULL,
    MetodoAsignacion VARCHAR(50) NOT NULL,
    FechaCreacion DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT chk_escenario_usuarios
        CHECK (CantidadUsuariosEstimados >= 0),

    CONSTRAINT chk_escenario_puntos
        CHECK (PuntosActivos >= 0),

    CONSTRAINT chk_escenario_horario
        CHECK (HoraFin > HoraInicio),

    CONSTRAINT chk_escenario_metodo
        CHECK (
            MetodoAsignacion IN (
                'Convencional',
                'Dinamica'
            )
        ),

    INDEX idx_escenarios_fecha (FechaCreacion),
    INDEX idx_escenarios_metodo (MetodoAsignacion)
) ENGINE = InnoDB;

-- ============================================================
-- 6. TABLA: TurnosSimulados
-- ============================================================

CREATE TABLE TurnosSimulados (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    CodigoTurno VARCHAR(20) NOT NULL,
    TramiteId INT NOT NULL,
    PuntoAtencionId INT NULL,
    EscenarioId INT NULL,
    FechaHoraSolicitud DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FechaHoraAtencion DATETIME NULL,
    TiempoEsperaEstimadoMinutos INT NOT NULL DEFAULT 0,
    Estado VARCHAR(20) NOT NULL DEFAULT 'En espera',

    CONSTRAINT fk_turno_tramite
        FOREIGN KEY (TramiteId)
        REFERENCES Tramites (Id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT fk_turno_punto
        FOREIGN KEY (PuntoAtencionId)
        REFERENCES PuntosAtencion (Id)
        ON UPDATE CASCADE
        ON DELETE SET NULL,

    CONSTRAINT fk_turno_escenario
        FOREIGN KEY (EscenarioId)
        REFERENCES EscenariosSimulacion (Id)
        ON UPDATE CASCADE
        ON DELETE SET NULL,

    CONSTRAINT chk_turno_espera
        CHECK (TiempoEsperaEstimadoMinutos >= 0),

    CONSTRAINT chk_turno_estado
        CHECK (
            Estado IN (
                'En espera',
                'Asignado',
                'En atencion',
                'Atendido',
                'Cancelado'
            )
        ),

    CONSTRAINT uq_turno_codigo
        UNIQUE (CodigoTurno),

    INDEX idx_turno_estado_fecha
        (Estado, FechaHoraSolicitud),

    INDEX idx_turno_punto_estado
        (PuntoAtencionId, Estado),

    INDEX idx_turno_escenario (EscenarioId)
) ENGINE = InnoDB;

-- ============================================================
-- 7. TABLA: TurnosAsignacionesHistorial
-- Guarda la asignacion inicial y las reasignaciones posteriores.
-- Se crea aqui para no depender de otro script SQL.
-- ============================================================

CREATE TABLE TurnosAsignacionesHistorial (
    Id BIGINT AUTO_INCREMENT PRIMARY KEY,
    TurnoSimuladoId INT NOT NULL,
    PuntoAtencionOrigenId INT NULL,
    PuntoAtencionDestinoId INT NULL,
    FechaHoraMovimiento DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    TipoMovimiento VARCHAR(30) NOT NULL,
    Motivo VARCHAR(200) NOT NULL,

    CONSTRAINT fk_historial_turno
        FOREIGN KEY (TurnoSimuladoId)
        REFERENCES TurnosSimulados (Id)
        ON UPDATE CASCADE
        ON DELETE CASCADE,

    CONSTRAINT fk_historial_origen
        FOREIGN KEY (PuntoAtencionOrigenId)
        REFERENCES PuntosAtencion (Id)
        ON UPDATE CASCADE
        ON DELETE SET NULL,

    CONSTRAINT fk_historial_destino
        FOREIGN KEY (PuntoAtencionDestinoId)
        REFERENCES PuntosAtencion (Id)
        ON UPDATE CASCADE
        ON DELETE SET NULL,

    CONSTRAINT chk_historial_tipo
        CHECK (
            TipoMovimiento IN (
                'Asignacion inicial',
                'Reasignacion'
            )
        ),

    INDEX idx_historial_turno_fecha
        (TurnoSimuladoId, FechaHoraMovimiento),

    INDEX idx_historial_fecha
        (FechaHoraMovimiento)
) ENGINE = InnoDB;

-- ============================================================
-- DATOS INICIALES: TRAMITES
-- ============================================================

INSERT INTO Tramites
    (Nombre, Descripcion, TiempoEstimadoMinutos, NivelDemanda, Estado)
VALUES
    (
        'Certificado',
        'Emision simulada de certificados registrales.',
        10,
        'Alta',
        'Activo'
    ),
    (
        'Rectificacion',
        'Revision y registro simulado de rectificaciones.',
        25,
        'Media',
        'Activo'
    ),
    (
        'Registro',
        'Atencion simulada de solicitudes de registro.',
        20,
        'Alta',
        'Activo'
    ),
    (
        'Legalizacion',
        'Verificacion y legalizacion simulada de documentos.',
        15,
        'Media',
        'Activo'
    );

-- ============================================================
-- DATOS INICIALES: PUNTOS DE ATENCION
-- ============================================================

INSERT INTO PuntosAtencion
    (Nombre, CargaActualPorcentaje, ColaUsuarios, Estado)
VALUES
    ('Ventanilla 01', 75, 8, 'Activa'),
    ('Ventanilla 02', 45, 4, 'Activa'),
    ('Ventanilla 03', 20, 2, 'Activa'),
    ('Ventanilla 04', 0, 0, 'Inactiva'),
    ('Ventanilla 05', 60, 6, 'Activa');

-- ============================================================
-- SERVICIOS COMPATIBLES POR VENTANILLA
-- ============================================================

INSERT INTO TramitePuntoAtencion
    (TramiteId, PuntoAtencionId)
VALUES
    -- Ventanilla 01: Certificados y registros.
    (1, 1),
    (3, 1),

    -- Ventanilla 02: Certificados, rectificaciones y registros.
    (1, 2),
    (2, 2),
    (3, 2),

    -- Ventanilla 03: Servicios generales.
    (1, 3),
    (2, 3),
    (3, 3),
    (4, 3),

    -- Ventanilla 04: Servicios habilitables.
    (1, 4),
    (3, 4),
    (4, 4),

    -- Ventanilla 05: Legalizaciones y rectificaciones.
    (2, 5),
    (4, 5);

-- ============================================================
-- DATOS INICIALES: DEMANDA SIMULADA
-- ============================================================

INSERT INTO RegistrosDemanda
    (FranjaHoraria, TramiteId, CantidadUsuarios, Fecha)
VALUES
    ('08:00-09:00', 1, 35, '2026-10-27'),
    ('08:00-09:00', 2, 12, '2026-10-27'),
    ('08:00-09:00', 3, 28, '2026-10-27'),
    ('08:00-09:00', 4, 10, '2026-10-27'),

    ('09:00-10:00', 1, 48, '2026-10-27'),
    ('09:00-10:00', 2, 18, '2026-10-27'),
    ('09:00-10:00', 3, 40, '2026-10-27'),
    ('09:00-10:00', 4, 16, '2026-10-27'),

    ('10:00-11:00', 1, 30, '2026-10-27'),
    ('10:00-11:00', 2, 10, '2026-10-27'),
    ('10:00-11:00', 3, 22, '2026-10-27'),
    ('10:00-11:00', 4, 8, '2026-10-27'),

    ('11:00-12:00', 1, 20, '2026-10-27'),
    ('11:00-12:00', 2, 8, '2026-10-27'),
    ('11:00-12:00', 3, 16, '2026-10-27'),
    ('11:00-12:00', 4, 6, '2026-10-27');

-- ============================================================
-- DATOS INICIALES: ESCENARIOS DE SIMULACION
-- ============================================================

INSERT INTO EscenariosSimulacion
    (
        Nombre,
        CantidadUsuariosEstimados,
        PuntosActivos,
        HoraInicio,
        HoraFin,
        MetodoAsignacion
    )
VALUES
    (
        'Jornada regular convencional',
        120,
        3,
        '08:00:00',
        '12:00:00',
        'Convencional'
    ),
    (
        'Hora pico convencional',
        180,
        3,
        '09:00:00',
        '12:00:00',
        'Convencional'
    ),
    (
        'Jornada regular dinamica',
        120,
        4,
        '08:00:00',
        '12:00:00',
        'Dinamica'
    ),
    (
        'Hora pico con reasignacion dinamica',
        180,
        5,
        '09:00:00',
        '12:00:00',
        'Dinamica'
    );

-- ============================================================
-- DATOS INICIALES: TURNOS FICTICIOS
-- ============================================================

INSERT INTO TurnosSimulados
    (
        CodigoTurno,
        TramiteId,
        PuntoAtencionId,
        EscenarioId,
        FechaHoraSolicitud,
        TiempoEsperaEstimadoMinutos,
        Estado
    )
VALUES
    (
        'DEMO-001',
        1,
        1,
        1,
        '2026-10-27 08:05:00',
        18,
        'Asignado'
    ),
    (
        'DEMO-002',
        2,
        2,
        1,
        '2026-10-27 08:10:00',
        25,
        'En espera'
    ),
    (
        'DEMO-003',
        3,
        3,
        1,
        '2026-10-27 08:15:00',
        12,
        'Asignado'
    ),
    (
        'DEMO-004',
        4,
        NULL,
        2,
        '2026-10-27 09:20:00',
        30,
        'En espera'
    ),
    (
        'DEMO-005',
        1,
        2,
        3,
        '2026-10-27 08:25:00',
        8,
        'Asignado'
    );

-- ============================================================
-- HISTORIAL INICIAL DE LOS TURNOS DE DEMOSTRACION
-- Registra las asignaciones existentes en los datos de ejemplo.
-- No genera movimientos para turnos sin ventanilla asignada.
-- ============================================================

INSERT INTO TurnosAsignacionesHistorial
    (
        TurnoSimuladoId,
        PuntoAtencionOrigenId,
        PuntoAtencionDestinoId,
        FechaHoraMovimiento,
        TipoMovimiento,
        Motivo
    )
SELECT
    ts.Id,
    NULL,
    ts.PuntoAtencionId,
    ts.FechaHoraSolicitud,
    'Asignacion inicial',
    'Asignacion inicial incluida en los datos de demostracion'
FROM TurnosSimulados AS ts
WHERE ts.PuntoAtencionId IS NOT NULL;

-- ============================================================
-- CONSULTAS DE VERIFICACION
-- ============================================================

-- 1. Ver tramites disponibles.
SELECT *
FROM Tramites
ORDER BY Id;

-- 2. Ver puntos de atencion y su carga.
SELECT *
FROM PuntosAtencion
ORDER BY Id;

-- 3. Ver servicios compatibles por ventanilla.
SELECT
    p.Nombre AS Ventanilla,
    t.Nombre AS Tramite,
    p.Estado AS EstadoVentanilla
FROM TramitePuntoAtencion AS tp
INNER JOIN PuntosAtencion AS p
    ON p.Id = tp.PuntoAtencionId
INNER JOIN Tramites AS t
    ON t.Id = tp.TramiteId
ORDER BY p.Id, t.Nombre;

-- 4. Ver demanda por franja horaria.
SELECT
    d.Fecha,
    d.FranjaHoraria,
    t.Nombre AS Tramite,
    d.CantidadUsuarios
FROM RegistrosDemanda AS d
INNER JOIN Tramites AS t
    ON t.Id = d.TramiteId
ORDER BY d.Fecha, d.FranjaHoraria, t.Id;

-- 5. Resumir demanda total por franja horaria.
SELECT
    Fecha,
    FranjaHoraria,
    SUM(CantidadUsuarios) AS TotalUsuarios
FROM RegistrosDemanda
GROUP BY Fecha, FranjaHoraria
ORDER BY Fecha, FranjaHoraria;

-- 6. Comparar escenarios por metodo de asignacion.
SELECT
    MetodoAsignacion,
    COUNT(*) AS CantidadEscenarios,
    SUM(CantidadUsuariosEstimados) AS UsuariosEstimados,
    AVG(PuntosActivos) AS PromedioPuntosActivos
FROM EscenariosSimulacion
GROUP BY MetodoAsignacion;

-- 7. Consultar turnos y estimaciones de espera.
SELECT
    ts.CodigoTurno,
    t.Nombre AS Tramite,
    p.Nombre AS VentanillaAsignada,
    es.Nombre AS Escenario,
    ts.TiempoEsperaEstimadoMinutos,
    ts.Estado
FROM TurnosSimulados AS ts
INNER JOIN Tramites AS t
    ON t.Id = ts.TramiteId
LEFT JOIN PuntosAtencion AS p
    ON p.Id = ts.PuntoAtencionId
LEFT JOIN EscenariosSimulacion AS es
    ON es.Id = ts.EscenarioId
ORDER BY ts.FechaHoraSolicitud;

-- 8. Verificar que exista la tabla del historial.
SHOW TABLES LIKE 'TurnosAsignacionesHistorial';

-- 9. Consultar historial de asignaciones (estara vacio inicialmente,
-- porque los turnos de demostracion no generan historial automaticamente).
SELECT *
FROM TurnosAsignacionesHistorial
ORDER BY FechaHoraMovimiento, Id;

-- ============================================================
-- FIN DEL SCRIPT UNIFICADO
-- ============================================================
