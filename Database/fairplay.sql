/* =====================================================================
   FAIRPLAY - Gestion de torneos de futbol
   Script de base de datos para SQLite (3.35 o superior)

   Formatos soportados: Liga, Copa, GruposCopa (grupos + eliminatoria), Suizo
   Estructura base:     Torneo > Fase > Ronda > Partido

   Convenciones
   - Fechas y horas: texto ISO-8601 ('2026-10-26' y '2026-10-26T15:30:00').
   - Booleanos: INTEGER 0/1.
   - Enumeraciones: TEXT + CHECK (en C# se mapean con HasConversion<string>()).
   - NO ACTION en SQLite se valida al final de la sentencia, por eso permite
     borrar un torneo completo (en cascada) aunque tenga partidos jugados.

   ADVERTENCIA: la seccion 1 BORRA todas las tablas de FairPlay.
   ===================================================================== */

PRAGMA foreign_keys = ON;   -- SQLite lo exige en CADA conexion (EF Core ya lo activa)

/* ---------------------------------------------------------------------
   1. LIMPIEZA (permite volver a ejecutar el script)
   --------------------------------------------------------------------- */
DROP VIEW    IF EXISTS vw_goleadores;
DROP VIEW    IF EXISTS vw_tabla_posiciones;
DROP TABLE   IF EXISTS cambio_estado_partido;
DROP TABLE   IF EXISTS evento_partido;
DROP TABLE   IF EXISTS partido;
DROP TABLE   IF EXISTS cruce;
DROP TABLE   IF EXISTS ronda;
DROP TABLE   IF EXISTS participante_fase;
DROP TABLE   IF EXISTS jugador;
DROP TABLE   IF EXISTS equipo;
DROP TABLE   IF EXISTS grupo;
DROP TABLE   IF EXISTS fase;
DROP TABLE   IF EXISTS torneo;
DROP TABLE   IF EXISTS usuario;

/* ---------------------------------------------------------------------
   2. TABLAS
   --------------------------------------------------------------------- */

-- USUARIO ------------------------------------------------------------
-- Si luego usas ASP.NET Core Identity, esta tabla la reemplaza AspNetUsers.
CREATE TABLE usuario (
    id_usuario      INTEGER PRIMARY KEY AUTOINCREMENT,
    nombre_usuario  TEXT    NOT NULL COLLATE NOCASE UNIQUE
        CHECK (length(nombre_usuario) BETWEEN 3 AND 30
               AND nombre_usuario NOT GLOB '*[^A-Za-z0-9_.-]*'),
    email           TEXT    NOT NULL COLLATE NOCASE UNIQUE
        CHECK (length(email) <= 254 AND email LIKE '_%@_%._%'),
    password_hash   TEXT    NOT NULL,                  -- nunca la contrasena en claro
    fecha_registro  TEXT    NOT NULL DEFAULT (datetime('now'))
);

-- TORNEO -------------------------------------------------------------
-- codigo: 8 caracteres de un alfabeto de 32 simbolos (sin I, O, 0, 1).
--         Lo genera la aplicacion con un generador criptografico.
CREATE TABLE torneo (
    id_torneo         INTEGER PRIMARY KEY AUTOINCREMENT,
    id_organizador    INTEGER NOT NULL
        REFERENCES usuario (id_usuario) ON DELETE RESTRICT,
    codigo            TEXT    NOT NULL UNIQUE
        CHECK (codigo GLOB '[A-HJ-NP-Z2-9][A-HJ-NP-Z2-9][A-HJ-NP-Z2-9][A-HJ-NP-Z2-9][A-HJ-NP-Z2-9][A-HJ-NP-Z2-9][A-HJ-NP-Z2-9][A-HJ-NP-Z2-9]'),
    nombre            TEXT    NOT NULL CHECK (length(trim(nombre)) BETWEEN 3 AND 100),
    descripcion       TEXT    CHECK (descripcion IS NULL OR length(descripcion) <= 500),
    formato           TEXT    NOT NULL
        CHECK (formato IN ('Liga', 'Copa', 'GruposCopa', 'Suizo')),
    estado            TEXT    NOT NULL DEFAULT 'Borrador'
        CHECK (estado IN ('Borrador', 'Inscripcion', 'EnCurso', 'Finalizado')),
    -- Cerrada: solo el organizador inscribe | ConAprobacion: otros usuarios
    -- inscriben y el organizador aprueba | Automatica: otros usuarios quedan aprobados
    modo_inscripcion  TEXT    NOT NULL DEFAULT 'Cerrada'
        CHECK (modo_inscripcion IN ('Cerrada', 'ConAprobacion', 'Automatica')),
    max_equipos       INTEGER CHECK (max_equipos IS NULL OR max_equipos >= 2),
    puntos_victoria   INTEGER NOT NULL DEFAULT 3 CHECK (puntos_victoria >= 0),
    puntos_empate     INTEGER NOT NULL DEFAULT 1 CHECK (puntos_empate   >= 0),
    puntos_derrota    INTEGER NOT NULL DEFAULT 0 CHECK (puntos_derrota  >= 0),
    fecha_inicio      TEXT,
    fecha_fin         TEXT,
    CHECK (fecha_inicio IS NULL OR fecha_fin IS NULL OR fecha_fin >= fecha_inicio)
);
CREATE INDEX ix_torneo_organizador ON torneo (id_organizador);

-- FASE ---------------------------------------------------------------
-- Liga: 1 fase Liga | Copa: 1 fase Eliminatoria |
-- GruposCopa: fase 1 Liga con grupos + fase 2 Eliminatoria | Suizo: 1 fase Suizo
CREATE TABLE fase (
    id_fase              INTEGER PRIMARY KEY AUTOINCREMENT,
    id_torneo            INTEGER NOT NULL
        REFERENCES torneo (id_torneo) ON DELETE CASCADE,
    orden                INTEGER NOT NULL CHECK (orden >= 1),
    nombre               TEXT    NOT NULL CHECK (length(trim(nombre)) BETWEEN 1 AND 100),
    tipo                 TEXT    NOT NULL CHECK (tipo IN ('Liga', 'Eliminatoria', 'Suizo')),
    ida_y_vuelta         INTEGER NOT NULL DEFAULT 0 CHECK (ida_y_vuelta IN (0, 1)),
    num_rondas           INTEGER CHECK (num_rondas IS NULL OR num_rondas >= 1),
    clasifican_por_grupo INTEGER CHECK (clasifican_por_grupo IS NULL OR clasifican_por_grupo >= 1),
    estado               TEXT    NOT NULL DEFAULT 'Pendiente'
        CHECK (estado IN ('Pendiente', 'EnCurso', 'Cerrada')),
    UNIQUE (id_torneo, orden),
    CHECK ((tipo = 'Suizo') = (num_rondas IS NOT NULL)),   -- num_rondas solo en Suizo
    CHECK (tipo <> 'Suizo' OR ida_y_vuelta = 0)            -- suizo: nunca se repite rival
);

-- GRUPO --------------------------------------------------------------
CREATE TABLE grupo (
    id_grupo  INTEGER PRIMARY KEY AUTOINCREMENT,
    id_fase   INTEGER NOT NULL REFERENCES fase (id_fase) ON DELETE CASCADE,
    nombre    TEXT    NOT NULL CHECK (length(trim(nombre)) BETWEEN 1 AND 50),
    UNIQUE (id_fase, nombre),
    UNIQUE (id_grupo, id_fase)            -- permite la clave foranea compuesta de RONDA
);

-- EQUIPO -------------------------------------------------------------
CREATE TABLE equipo (
    id_equipo           INTEGER PRIMARY KEY AUTOINCREMENT,
    id_torneo           INTEGER NOT NULL
        REFERENCES torneo (id_torneo) ON DELETE CASCADE,
    id_responsable      INTEGER NOT NULL            -- usuario que inscribio el equipo
        REFERENCES usuario (id_usuario) ON DELETE RESTRICT,
    nombre              TEXT    NOT NULL COLLATE NOCASE
        CHECK (length(trim(nombre)) BETWEEN 2 AND 60),
    ciudad              TEXT    CHECK (ciudad IS NULL OR length(ciudad) <= 60),
    logo_escudo         TEXT,                       -- ruta relativa del archivo (wwwroot/...)
    estado_inscripcion  TEXT    NOT NULL DEFAULT 'Pendiente'
        CHECK (estado_inscripcion IN ('Pendiente', 'Aprobado', 'Rechazado')),
    UNIQUE (id_torneo, nombre)
);
CREATE INDEX ix_equipo_responsable ON equipo (id_responsable);

-- JUGADOR ------------------------------------------------------------
-- Posiciones y rango de dorsal segun la guia de CRUD de jugadores.
CREATE TABLE jugador (
    id_jugador        INTEGER PRIMARY KEY AUTOINCREMENT,
    id_equipo         INTEGER NOT NULL
        REFERENCES equipo (id_equipo) ON DELETE CASCADE,
    nombre            TEXT    NOT NULL CHECK (length(trim(nombre))   BETWEEN 1 AND 60),
    apellido          TEXT    NOT NULL CHECK (length(trim(apellido)) BETWEEN 1 AND 60),
    fecha_nacimiento  TEXT    NOT NULL CHECK (fecha_nacimiento GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
    posicion          TEXT    NOT NULL
        CHECK (posicion IN ('Arquero', 'Defensa', 'Medio campo', 'Delantero')),
    dorsal            INTEGER NOT NULL CHECK (dorsal BETWEEN 1 AND 99),
    es_capitan        INTEGER NOT NULL DEFAULT 0 CHECK (es_capitan IN (0, 1)),
    UNIQUE (id_equipo, dorsal)
);
-- A lo sumo un capitan por equipo (indice unico parcial)
CREATE UNIQUE INDEX uq_jugador_capitan ON jugador (id_equipo) WHERE es_capitan = 1;

-- PARTICIPANTE_FASE --------------------------------------------------
-- Que equipos juegan cada fase, en que grupo y si clasificaron a la siguiente.
CREATE TABLE participante_fase (
    id_participante  INTEGER PRIMARY KEY AUTOINCREMENT,
    id_fase          INTEGER NOT NULL REFERENCES fase (id_fase)     ON DELETE CASCADE,
    id_equipo        INTEGER NOT NULL REFERENCES equipo (id_equipo) ON DELETE NO ACTION,
    id_grupo         INTEGER          REFERENCES grupo (id_grupo)   ON DELETE SET NULL,
    semilla          INTEGER CHECK (semilla IS NULL OR semilla >= 1),
    clasificado      INTEGER NOT NULL DEFAULT 0 CHECK (clasificado IN (0, 1)),
    UNIQUE (id_fase, id_equipo)
);
CREATE INDEX ix_participante_equipo ON participante_fase (id_equipo);
CREATE INDEX ix_participante_grupo  ON participante_fase (id_grupo);

-- RONDA --------------------------------------------------------------
-- Jornada de liga, ronda suiza o ronda de eliminatoria (octavos, cuartos...).
CREATE TABLE ronda (
    id_ronda      INTEGER PRIMARY KEY AUTOINCREMENT,
    id_fase       INTEGER NOT NULL REFERENCES fase (id_fase) ON DELETE CASCADE,
    id_grupo      INTEGER,
    numero        INTEGER NOT NULL CHECK (numero >= 1),
    nombre        TEXT    NOT NULL CHECK (length(trim(nombre)) BETWEEN 1 AND 60),
    fecha_inicio  TEXT,
    -- el grupo debe pertenecer a la MISMA fase que la ronda
    FOREIGN KEY (id_grupo, id_fase) REFERENCES grupo (id_grupo, id_fase) ON DELETE CASCADE
);
-- numero unico por fase y grupo (IFNULL evita que los NULL se consideren distintos)
CREATE UNIQUE INDEX uq_ronda_numero ON ronda (id_fase, IFNULL(id_grupo, 0), numero);
CREATE INDEX ix_ronda_grupo ON ronda (id_grupo, id_fase);

-- CRUCE --------------------------------------------------------------
-- Enfrentamiento de eliminatoria (puede tener 1 partido o 2 si es ida y vuelta).
CREATE TABLE cruce (
    id_cruce             INTEGER PRIMARY KEY AUTOINCREMENT,
    id_ronda             INTEGER NOT NULL REFERENCES ronda (id_ronda) ON DELETE CASCADE,
    posicion             INTEGER NOT NULL CHECK (posicion >= 1),
    id_equipo_a          INTEGER REFERENCES equipo (id_equipo) ON DELETE NO ACTION,
    id_equipo_b          INTEGER REFERENCES equipo (id_equipo) ON DELETE NO ACTION,
    id_ganador           INTEGER REFERENCES equipo (id_equipo) ON DELETE NO ACTION,
    id_cruce_siguiente   INTEGER REFERENCES cruce  (id_cruce)  ON DELETE SET NULL,
    UNIQUE (id_ronda, posicion),
    CHECK (id_equipo_a <> id_equipo_b),
    CHECK (id_ganador IS NULL OR id_ganador = id_equipo_a OR id_ganador = id_equipo_b),
    CHECK (id_cruce_siguiente IS NULL OR id_cruce_siguiente <> id_cruce)
);
CREATE INDEX ix_cruce_equipo_a   ON cruce (id_equipo_a);
CREATE INDEX ix_cruce_equipo_b   ON cruce (id_equipo_b);
CREATE INDEX ix_cruce_ganador    ON cruce (id_ganador);
CREATE INDEX ix_cruce_siguiente  ON cruce (id_cruce_siguiente);

-- PARTIDO ------------------------------------------------------------
-- id_visitante NULL = el equipo local descansa (liga impar o "bye" suizo).
-- Un descanso suizo se guarda con estado 'Finalizado' y goles NULL.
-- Los goles son NULL mientras el partido esta 'Programado'.
CREATE TABLE partido (
    id_partido        INTEGER PRIMARY KEY AUTOINCREMENT,
    id_ronda          INTEGER NOT NULL REFERENCES ronda (id_ronda)   ON DELETE CASCADE,
    id_cruce          INTEGER          REFERENCES cruce (id_cruce)   ON DELETE CASCADE,
    id_arbitro        INTEGER          REFERENCES usuario (id_usuario) ON DELETE SET NULL,
    numero_partido    INTEGER NOT NULL DEFAULT 1 CHECK (numero_partido IN (1, 2)),  -- 1 ida / 2 vuelta
    id_local          INTEGER NOT NULL REFERENCES equipo (id_equipo) ON DELETE NO ACTION,
    id_visitante      INTEGER          REFERENCES equipo (id_equipo) ON DELETE NO ACTION,
    fecha_hora        TEXT,
    estado 	           TEXT    NOT NULL DEFAULT 'Programado'
        CHECK (estado IN ('Programado', 'EnVivo', 'Finalizado', 'Suspendido')),
    goles_local       INTEGER CHECK (goles_local       IS NULL OR goles_local       >= 0),
    goles_visitante   INTEGER CHECK (goles_visitante   IS NULL OR goles_visitante   >= 0),
    penales_local     INTEGER CHECK (penales_local     IS NULL OR penales_local     >= 0),
    penales_visitante INTEGER CHECK (penales_visitante IS NULL OR penales_visitante >= 0),
    UNIQUE (id_cruce, numero_partido),
    CHECK (id_local <> id_visitante),
    CHECK ((penales_local IS NULL) = (penales_visitante IS NULL)),
    CHECK ((goles_local   IS NULL) = (goles_visitante   IS NULL)),
    -- EnVivo o Finalizado exigen marcador (los descansos no tienen rival ni marcador)
    CHECK (id_visitante IS NULL
           OR estado NOT IN ('EnVivo', 'Finalizado')
           OR goles_local IS NOT NULL),
    CHECK (id_visitante IS NOT NULL OR (goles_local IS NULL AND penales_local IS NULL))
);
CREATE INDEX ix_partido_ronda      ON partido (id_ronda);
CREATE INDEX ix_partido_arbitro    ON partido (id_arbitro);
CREATE INDEX ix_partido_local      ON partido (id_local);
CREATE INDEX ix_partido_visitante  ON partido (id_visitante);

-- CAMBIO_ESTADO_PARTIDO (historial de auditoria) ----------------------
CREATE TABLE cambio_estado_partido (
    id_cambio        INTEGER PRIMARY KEY AUTOINCREMENT,
    id_partido       INTEGER NOT NULL REFERENCES partido (id_partido) ON DELETE CASCADE,
    id_usuario       INTEGER          REFERENCES usuario (id_usuario) ON DELETE SET NULL,
    estado_anterior  TEXT    NOT NULL
        CHECK (estado_anterior IN ('Programado', 'EnVivo', 'Finalizado', 'Suspendido')),
    estado_nuevo     TEXT    NOT NULL
        CHECK (estado_nuevo    IN ('Programado', 'EnVivo', 'Finalizado', 'Suspendido')),
    fecha_hora       TEXT    NOT NULL DEFAULT (datetime('now')),
    CHECK (estado_anterior <> estado_nuevo)
);
CREATE INDEX ix_cambio_partido ON cambio_estado_partido (id_partido, fecha_hora);
CREATE INDEX ix_cambio_usuario ON cambio_estado_partido (id_usuario);

-- EVENTO_PARTIDO -----------------------------------------------------
-- id_equipo = equipo del jugador que protagoniza el evento. En un 'Gol' sin
-- jugador (el arbitro solo suma al marcador) es el equipo al que se acredita.
-- En un 'Autogol' es el equipo del jugador; el marcador lo suma al rival.
CREATE TABLE evento_partido (
    id_evento          INTEGER PRIMARY KEY AUTOINCREMENT,
    id_partido         INTEGER NOT NULL REFERENCES partido (id_partido) ON DELETE CASCADE,
    id_equipo          INTEGER NOT NULL REFERENCES equipo  (id_equipo)  ON DELETE NO ACTION,
    id_jugador         INTEGER          REFERENCES jugador (id_jugador) ON DELETE NO ACTION,
    id_registrado_por  INTEGER          REFERENCES usuario (id_usuario) ON DELETE SET NULL,
    tipo               TEXT    NOT NULL CHECK (tipo IN ('Gol', 'Autogol', 'Amarilla', 'Roja')),
    minuto             INTEGER CHECK (minuto IS NULL OR minuto BETWEEN 0 AND 130),
    CHECK (tipo = 'Gol' OR id_jugador IS NOT NULL)
);
CREATE INDEX ix_evento_partido    ON evento_partido (id_partido);
CREATE INDEX ix_evento_equipo     ON evento_partido (id_equipo);
CREATE INDEX ix_evento_jugador    ON evento_partido (id_jugador);
CREATE INDEX ix_evento_registrado ON evento_partido (id_registrado_por);

/* ---------------------------------------------------------------------
   3. TRIGGERS (integridad que un CHECK no puede expresar)
   Los PERMISOS por rol (organizador / responsable / arbitro) se validan en
   la aplicacion; aqui solo se protege la coherencia de los datos.
   --------------------------------------------------------------------- */

-- 3.1 Maquina de estados del partido
--     Programado -> EnVivo | Suspendido      EnVivo -> Finalizado | Suspendido
--     Finalizado -> EnVivo (reabrir)         Suspendido -> Programado
CREATE TRIGGER trg_partido_transicion
BEFORE UPDATE OF estado ON partido
FOR EACH ROW
WHEN NEW.estado <> OLD.estado
 AND NOT (   (OLD.estado = 'Programado' AND NEW.estado IN ('EnVivo', 'Suspendido'))
          OR (OLD.estado = 'EnVivo'     AND NEW.estado IN ('Finalizado', 'Suspendido'))
          OR (OLD.estado = 'Finalizado' AND NEW.estado = 'EnVivo')
          OR (OLD.estado = 'Suspendido' AND NEW.estado = 'Programado'))
BEGIN
    SELECT RAISE(ABORT, 'Transicion de estado de partido no permitida');
END;

-- 3.2 Los equipos de un partido deben pertenecer al torneo de su ronda
CREATE TRIGGER trg_partido_equipos_ins
BEFORE INSERT ON partido
FOR EACH ROW
WHEN NOT EXISTS (SELECT 1 FROM ronda r JOIN fase f ON f.id_fase = r.id_fase
                 JOIN equipo e ON e.id_torneo = f.id_torneo
                 WHERE r.id_ronda = NEW.id_ronda AND e.id_equipo = NEW.id_local)
  OR (NEW.id_visitante IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM ronda r JOIN fase f ON f.id_fase = r.id_fase
                      JOIN equipo e ON e.id_torneo = f.id_torneo
                      WHERE r.id_ronda = NEW.id_ronda AND e.id_equipo = NEW.id_visitante))
BEGIN
    SELECT RAISE(ABORT, 'Los equipos del partido deben pertenecer al torneo de la ronda');
END;

CREATE TRIGGER trg_partido_equipos_upd
BEFORE UPDATE OF id_ronda, id_local, id_visitante ON partido
FOR EACH ROW
WHEN NOT EXISTS (SELECT 1 FROM ronda r JOIN fase f ON f.id_fase = r.id_fase
                 JOIN equipo e ON e.id_torneo = f.id_torneo
                 WHERE r.id_ronda = NEW.id_ronda AND e.id_equipo = NEW.id_local)
  OR (NEW.id_visitante IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM ronda r JOIN fase f ON f.id_fase = r.id_fase
                      JOIN equipo e ON e.id_torneo = f.id_torneo
                      WHERE r.id_ronda = NEW.id_ronda AND e.id_equipo = NEW.id_visitante))
BEGIN
    SELECT RAISE(ABORT, 'Los equipos del partido deben pertenecer al torneo de la ronda');
END;

-- 3.3 El grupo de un participante debe pertenecer a su misma fase
CREATE TRIGGER trg_participante_grupo_ins
BEFORE INSERT ON participante_fase
FOR EACH ROW
WHEN NEW.id_grupo IS NOT NULL
 AND NOT EXISTS (SELECT 1 FROM grupo g WHERE g.id_grupo = NEW.id_grupo AND g.id_fase = NEW.id_fase)
BEGIN
    SELECT RAISE(ABORT, 'El grupo no pertenece a la fase del participante');
END;

CREATE TRIGGER trg_participante_grupo_upd
BEFORE UPDATE OF id_fase, id_grupo ON participante_fase
FOR EACH ROW
WHEN NEW.id_grupo IS NOT NULL
 AND NOT EXISTS (SELECT 1 FROM grupo g WHERE g.id_grupo = NEW.id_grupo AND g.id_fase = NEW.id_fase)
BEGIN
    SELECT RAISE(ABORT, 'El grupo no pertenece a la fase del participante');
END;

-- 3.4 Eventos: el equipo debe jugar el partido y el jugador pertenecer a ese equipo
CREATE TRIGGER trg_evento_integridad_ins
BEFORE INSERT ON evento_partido
FOR EACH ROW
WHEN NOT EXISTS (SELECT 1 FROM partido p WHERE p.id_partido = NEW.id_partido
                 AND NEW.id_equipo IN (p.id_local, IFNULL(p.id_visitante, -1)))
  OR (NEW.id_jugador IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM jugador j WHERE j.id_jugador = NEW.id_jugador
                      AND j.id_equipo = NEW.id_equipo))
BEGIN
    SELECT RAISE(ABORT, 'Evento invalido: equipo o jugador no corresponden al partido');
END;

CREATE TRIGGER trg_evento_integridad_upd
BEFORE UPDATE OF id_partido, id_equipo, id_jugador ON evento_partido
FOR EACH ROW
WHEN NOT EXISTS (SELECT 1 FROM partido p WHERE p.id_partido = NEW.id_partido
                 AND NEW.id_equipo IN (p.id_local, IFNULL(p.id_visitante, -1)))
  OR (NEW.id_jugador IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM jugador j WHERE j.id_jugador = NEW.id_jugador
                      AND j.id_equipo = NEW.id_equipo))
BEGIN
    SELECT RAISE(ABORT, 'Evento invalido: equipo o jugador no corresponden al partido');
END;

/* ---------------------------------------------------------------------
   4. VISTAS
   --------------------------------------------------------------------- */

-- Tabla de posiciones por fase y grupo (id_grupo NULL = fase sin grupos).
-- Solo cuenta partidos 'Finalizado'. Los puntos salen de la configuracion del
-- torneo. En fases Suizo, el descanso (bye) cuenta como victoria sin goles.
-- Ordenar al consultar:  ORDER BY puntos DESC, dg DESC, gf DESC
CREATE VIEW vw_tabla_posiciones AS
SELECT  x.id_fase,
        x.id_grupo,
        x.id_equipo,
        e.nombre                                   AS equipo,
        COUNT(*)                                   AS pj,
        SUM(x.res = 'G')                           AS pg,
        SUM(x.res = 'E')                           AS pe,
        SUM(x.res = 'P')                           AS pp,
        SUM(x.gf)                                  AS gf,
        SUM(x.gc)                                  AS gc,
        SUM(x.gf - x.gc)                           AS dg,
        SUM(CASE x.res WHEN 'G' THEN t.puntos_victoria
                       WHEN 'E' THEN t.puntos_empate
                       ELSE          t.puntos_derrota END) AS puntos
FROM (
    SELECT r.id_fase, r.id_grupo, p.id_local AS id_equipo,
           p.goles_local AS gf, p.goles_visitante AS gc,
           CASE WHEN p.goles_local > p.goles_visitante THEN 'G'
                WHEN p.goles_local = p.goles_visitante THEN 'E' ELSE 'P' END AS res
    FROM partido p JOIN ronda r ON r.id_ronda = p.id_ronda
    WHERE p.estado = 'Finalizado' AND p.id_visitante IS NOT NULL
    UNION ALL
    SELECT r.id_fase, r.id_grupo, p.id_visitante,
           p.goles_visitante, p.goles_local,
           CASE WHEN p.goles_visitante > p.goles_local THEN 'G'
                WHEN p.goles_visitante = p.goles_local THEN 'E' ELSE 'P' END
    FROM partido p JOIN ronda r ON r.id_ronda = p.id_ronda
    WHERE p.estado = 'Finalizado' AND p.id_visitante IS NOT NULL
    UNION ALL
    SELECT r.id_fase, r.id_grupo, p.id_local, 0, 0, 'G'
    FROM partido p JOIN ronda r ON r.id_ronda = p.id_ronda
                   JOIN fase  f ON f.id_fase  = r.id_fase
    WHERE p.estado = 'Finalizado' AND p.id_visitante IS NULL AND f.tipo = 'Suizo'
) AS x
JOIN equipo e ON e.id_equipo = x.id_equipo
JOIN fase   f ON f.id_fase   = x.id_fase
JOIN torneo t ON t.id_torneo = f.id_torneo
GROUP BY x.id_fase, x.id_grupo, x.id_equipo, e.nombre;

-- Goleadores por torneo (solo goles con jugador asignado)
CREATE VIEW vw_goleadores AS
SELECT  e.id_torneo,
        j.id_jugador,
        j.nombre || ' ' || j.apellido AS jugador,
        e.id_equipo,
        e.nombre                      AS equipo,
        COUNT(*)                      AS goles
FROM evento_partido ev
JOIN jugador j ON j.id_jugador = ev.id_jugador
JOIN equipo  e ON e.id_equipo  = j.id_equipo
WHERE ev.tipo = 'Gol'
GROUP BY e.id_torneo, j.id_jugador, j.nombre, j.apellido, e.id_equipo, e.nombre;

/* ---------------------------------------------------------------------
   5. DATOS DE EJEMPLO (OPCIONAL)
   Quita el comentario de este bloque para probar el esquema y las vistas.
   --------------------------------------------------------------------- */

INSERT INTO usuario (nombre_usuario, email, password_hash) VALUES
    ('organizador1', 'org@ejemplo.com',  'HASH_DE_EJEMPLO'),
    ('arbitro1',     'arb@ejemplo.com',  'HASH_DE_EJEMPLO'),
    ('delegado1',    'del@ejemplo.com',  'HASH_DE_EJEMPLO');

INSERT INTO torneo (id_organizador, codigo, nombre, formato, estado, modo_inscripcion, max_equipos)
VALUES (1, 'K7M2QX9P', 'Liga Interbarrial 2026', 'Liga', 'EnCurso', 'ConAprobacion', 8);

INSERT INTO fase (id_torneo, orden, nombre, tipo, ida_y_vuelta, estado)
VALUES (1, 1, 'Fase unica', 'Liga', 0, 'EnCurso');

INSERT INTO equipo (id_torneo, id_responsable, nombre, ciudad, estado_inscripcion) VALUES
    (1, 1, 'Los Halcones', 'La Paz',     'Aprobado'),
    (1, 3, 'Union Sur',    'La Paz',     'Aprobado'),
    (1, 3, 'Deportivo Sol','El Alto',    'Aprobado');

INSERT INTO jugador (id_equipo, nombre, apellido, fecha_nacimiento, posicion, dorsal, es_capitan) VALUES
    (1, 'Juan',  'Mamani', '2000-03-14', 'Delantero',   9, 1),
    (1, 'Luis',  'Quispe', '1999-07-02', 'Arquero',     1, 0),
    (2, 'Pedro', 'Rojas',  '2001-11-20', 'Medio campo', 8, 1),
    (2, 'Raul',  'Flores', '2002-01-05', 'Delantero',  10, 0),
    (3, 'Mario', 'Choque', '1998-05-30', 'Defensa',     4, 1),
    (3, 'Hugo',  'Vargas', '2000-09-09', 'Delantero',  11, 0);

INSERT INTO participante_fase (id_fase, id_equipo) VALUES (1,1),(1,2),(1,3);

INSERT INTO ronda (id_fase, numero, nombre) VALUES
    (1, 1, 'Jornada 1'), (1, 2, 'Jornada 2'), (1, 3, 'Jornada 3');

-- Jornada 1: Halcones vs Union Sur (Deportivo Sol descansa)
INSERT INTO partido (id_ronda, id_arbitro, id_local, id_visitante, estado, goles_local, goles_visitante)
VALUES (1, 2, 1, 2, 'Programado', NULL, NULL);
-- Maquina de estados: Programado -> EnVivo (marcador en 0) -> Finalizado
UPDATE partido SET estado = 'EnVivo', goles_local = 0, goles_visitante = 0 WHERE id_partido = 1;
INSERT INTO evento_partido (id_partido, id_equipo, id_jugador, id_registrado_por, tipo, minuto) VALUES
    (1, 1, 1, 2, 'Gol', 12), (1, 2, 4, 2, 'Gol', 40), (1, 1, 1, 2, 'Gol', 77), (1, 2, 3, 2, 'Amarilla', 55);
UPDATE partido SET goles_local = 2, goles_visitante = 1 WHERE id_partido = 1;
UPDATE partido SET estado = 'Finalizado' WHERE id_partido = 1;
INSERT INTO cambio_estado_partido (id_partido, id_usuario, estado_anterior, estado_nuevo) VALUES
    (1, 2, 'Programado', 'EnVivo'), (1, 2, 'EnVivo', 'Finalizado');

-- Jornadas 2 y 3 (ya jugadas)
INSERT INTO partido (id_ronda, id_local, id_visitante, estado, goles_local, goles_visitante) VALUES
    (2, 1, 3, 'Finalizado', 0, 0),
    (3, 2, 3, 'Finalizado', 3, 1);

SELECT * FROM vw_tabla_posiciones ORDER BY puntos DESC, dg DESC, gf DESC;
SELECT * FROM vw_goleadores ORDER BY goles DESC;