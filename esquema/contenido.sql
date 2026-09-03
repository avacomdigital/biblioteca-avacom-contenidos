-- ============================================================
-- AVACOM · esquema del componente de contenido educativo
--
-- Ocho tablas y tres vistas. Es TODO lo que este componente
-- escribe en disco, y no depende de ninguna otra base.
--
-- Lo que hay aqui NO es el LMS. El LMS lleva personas, grupos,
-- matriculas, intentos y calificaciones, tiene su propio
-- esquema y lo desarrolla otro equipo. Este componente es la
-- biblioteca: sabe que material hay instalado, sabe abrirlo, y
-- apunta que se abrio. Nada mas.
--
-- Se aplica solo, la primera vez que la aplicacion arranca con
-- una carpeta de trabajo. No hace falta ejecutarlo a mano.
-- ============================================================


-- ------------------------------------------------------------
-- Que paquetes hay instalados en este equipo
-- ------------------------------------------------------------
CREATE TABLE m04_paquete_instalado (
  id                         TEXT     PRIMARY KEY,
  clave_paquete              TEXT     NOT NULL,
  version                    TEXT     NOT NULL,
  formato_version            INTEGER  NOT NULL,
  origen                     TEXT     NOT NULL CHECK(origen IN ('avacom','escuela')),
  pais                       TEXT,
  nivel_clave                TEXT,
  grado                      TEXT,
  asignatura                 TEXT,
  idioma                     TEXT,
  ruta_paquete               TEXT     NOT NULL,
  huella_manifiesto          TEXT     NOT NULL,
  firma_verificada           INTEGER  NOT NULL DEFAULT 0,
  instalado_en               INTEGER  NOT NULL,
  estado                     TEXT     NOT NULL DEFAULT 'activo'
                                      CHECK(estado IN ('activo','desactivado','no_verificable','retirado')),
  creado_en                  INTEGER  NOT NULL,
  creado_por                 TEXT,
  secuencia                  INTEGER  NOT NULL
);
CREATE UNIQUE INDEX ux_m04_paq ON m04_paquete_instalado(clave_paquete, version);


-- ------------------------------------------------------------
-- Proyeccion del catalogo de los paquetes instalados
--
-- Esto NO es fuente de verdad de nada. El manifiesto de cada
-- paquete manda; si esta tabla y el manifiesto discrepan, la
-- que se equivoca es esta tabla. Si se pierde, se reconstruye
-- escaneando los paquetes y da exactamente lo mismo.
--
-- En cuanto se le añada aqui un dato que no venga de ningun
-- paquete, deja de poder reconstruirse y se convierte en un
-- segundo catalogo que hay que mantener sincronizado a mano.
-- ------------------------------------------------------------
CREATE TABLE m04_indice_elemento (
  elemento_ref               TEXT     NOT NULL,
  paquete_id                 TEXT     NOT NULL,
  version_elemento           TEXT     NOT NULL,
  tipo                       TEXT     NOT NULL,
  titulo                     TEXT     NOT NULL,
  taxonomia_ref              TEXT,
  nivel_clave                TEXT,
  grado                      TEXT,
  asignatura                 TEXT,
  idioma                     TEXT,
  huella_archivo             TEXT,
  duracion_seg               INTEGER,
  estado                     TEXT     NOT NULL DEFAULT 'vigente',
  sucesor_ref                TEXT,
  FOREIGN KEY(paquete_id) REFERENCES m04_paquete_instalado(id)
);
CREATE UNIQUE INDEX ux_m04_idx_elem ON m04_indice_elemento(elemento_ref);
CREATE INDEX ix_m04_idx_busq ON m04_indice_elemento(nivel_clave, grado, asignatura, tipo);
CREATE INDEX ix_m04_idx_tax ON m04_indice_elemento(taxonomia_ref);


-- Proyeccion de la estructura curricular que traen los paquetes.
-- Tambien reconstruible, y por el mismo motivo: la taxonomia la
-- define el contenido, no este componente.
CREATE TABLE m04_indice_taxonomia (
  taxonomia_ref              TEXT     NOT NULL PRIMARY KEY,
  paquete_id                 TEXT     NOT NULL,
  padre_ref                  TEXT,
  tipo_nodo                  TEXT     NOT NULL,
  codigo                     TEXT,
  nombre                     TEXT     NOT NULL,
  orden                      INTEGER  NOT NULL,
  pais                       TEXT,
  nivel_clave                TEXT
);
CREATE INDEX ix_m04_idx_tax_padre ON m04_indice_taxonomia(padre_ref, orden);


-- Estado de la proyeccion: declara su antiguedad y si hace falta rehacerla.
CREATE TABLE m04_indice_estado (
  id                         INTEGER  PRIMARY KEY CHECK(id = 1),
  paquetes_indexados         INTEGER  NOT NULL DEFAULT 0,
  elementos_indexados        INTEGER  NOT NULL DEFAULT 0,
  estado                     TEXT     NOT NULL DEFAULT 'al_dia'
                                      CHECK(estado IN ('al_dia','reconstruyendo','incompleto')),
  reconstruido_en            INTEGER
);


-- ------------------------------------------------------------
-- La consola del administrador
--
-- Se aplica ENCIMA del catalogo, sin modificarlo. Asi una
-- actualizacion de contenido nunca pisa una decision de la
-- escuela, y retirar la regla devuelve el catalogo entero sin
-- tener que reinstalar nada.
--
-- Los seis ambitos de aqui abajo son los unicos que el codigo
-- reconoce. Si se añade uno a esta lista sin tocar
-- Politica.Permite, la regla se guarda, se lista en pantalla y
-- no filtra nada.
-- ------------------------------------------------------------
CREATE TABLE m04_politica (
  id                         TEXT     PRIMARY KEY,
  ambito                     TEXT     NOT NULL
                                      CHECK(ambito IN ('paquete','nivel','grado','asignatura','taxonomia','elemento')),
  ambito_valor               TEXT     NOT NULL,
  accion                     TEXT     NOT NULL CHECK(accion IN ('habilitar','deshabilitar','fijar_version')),
  version_fijada             TEXT,
  grupo_id                   TEXT,
  motivo                     TEXT,
  vigente_desde              INTEGER  NOT NULL,
  vigente_hasta              INTEGER,
  creado_en                  INTEGER  NOT NULL,
  creado_por                 TEXT,
  secuencia                  INTEGER  NOT NULL
);
CREATE INDEX ix_m04_pol ON m04_politica(ambito, ambito_valor, vigente_hasta);


-- Que referencias ha usado de verdad esta instalacion. Permite explicar
-- que se mostro en una clase aunque el paquete se haya desinstalado despues.
CREATE TABLE m04_referencia_usada (
  elemento_ref               TEXT     NOT NULL PRIMARY KEY,
  version_elemento           TEXT     NOT NULL,
  huella_archivo             TEXT,
  titulo_al_usar             TEXT     NOT NULL,
  primera_vez_en             INTEGER  NOT NULL,
  ultima_vez_en              INTEGER  NOT NULL,
  usos                       INTEGER  NOT NULL DEFAULT 1
);


-- ------------------------------------------------------------
-- Modo repaso
--
-- El alumno accede al contenido por su cuenta. NO genera
-- intento, NO genera calificacion, NO alimenta el dominio. Solo
-- queda constancia de que se abrio algo y cuanto tiempo.
--
-- persona_id ADMITE NULO, y es a proposito.
--
-- Esta biblioteca es abierta: alguien puede sentarse en la
-- pantalla y consultar material sin identificarse, y en
-- preescolar directamente no hay con que identificarse. Quien
-- sabe quien es cada persona es el LMS, que tiene su propia
-- tabla y sus propias reglas. Si aqui se exigiera una persona,
-- el componente no podria funcionar solo, que es justo lo que
-- tiene que poder hacer.
--
-- Cuando el LMS esta presente, escribe aqui su identificador y
-- la relacion se resuelve del lado del LMS. Este componente no
-- declara esa clave foranea porque no es dueño de esa tabla, y
-- declararla obligaria a arrastrar el esquema entero del LMS
-- para poder crear dos tablas.
-- ------------------------------------------------------------
CREATE TABLE m08_repaso_sesion (
  id                         TEXT     PRIMARY KEY,
  persona_id                 TEXT,
  dispositivo_id             TEXT,
  iniciada_en                INTEGER  NOT NULL,
  terminada_en               INTEGER,
  creado_en                  INTEGER  NOT NULL,
  creado_por                 TEXT,
  secuencia                  INTEGER  NOT NULL
);
CREATE INDEX ix_m08_rep ON m08_repaso_sesion(persona_id, iniciada_en);


-- Que contenido abrio y cuanto tiempo estuvo. Es todo lo que el repaso deja.
CREATE TABLE m08_repaso_consumo (
  id                         TEXT     PRIMARY KEY,
  repaso_sesion_id           TEXT     NOT NULL,
  elemento_ref               TEXT     NOT NULL,
  version_elemento           TEXT     NOT NULL,
  abierto_en                 INTEGER  NOT NULL,
  cerrado_en                 INTEGER,
  segundos                   INTEGER,
  progreso_pct               INTEGER,
  creado_en                  INTEGER  NOT NULL,
  creado_por                 TEXT,
  secuencia                  INTEGER  NOT NULL,
  FOREIGN KEY(repaso_sesion_id) REFERENCES m08_repaso_sesion(id)
);
CREATE INDEX ix_m08_repcon ON m08_repaso_consumo(repaso_sesion_id);
CREATE INDEX ix_m08_repcon_elem ON m08_repaso_consumo(elemento_ref);


-- ============================================================
-- Vistas
-- ============================================================

-- Lo que esta disponible de verdad en esta aula, ya filtrado por la politica.
CREATE VIEW v_contenido_disponible AS
SELECT i.elemento_ref, i.titulo, i.tipo, i.nivel_clave, i.grado, i.asignatura,
       i.taxonomia_ref, i.version_elemento, p.origen, p.clave_paquete
FROM m04_indice_elemento i
JOIN m04_paquete_instalado p ON p.id = i.paquete_id
WHERE p.estado = 'activo'
  AND i.estado = 'vigente'
  AND NOT EXISTS (
    SELECT 1 FROM m04_politica pol
    WHERE pol.accion = 'deshabilitar'
      AND (pol.vigente_hasta IS NULL OR pol.vigente_hasta > 0)
      AND ( (pol.ambito='elemento'   AND pol.ambito_valor = i.elemento_ref)
         OR (pol.ambito='paquete'    AND pol.ambito_valor = p.clave_paquete)
         OR (pol.ambito='nivel'      AND pol.ambito_valor = i.nivel_clave)
         OR (pol.ambito='grado'      AND pol.ambito_valor = i.grado)
         OR (pol.ambito='asignatura' AND pol.ambito_valor = i.asignatura)
         OR (pol.ambito='taxonomia'  AND pol.ambito_valor = i.taxonomia_ref) )
  );

-- Contenido que preparo la propia escuela, separado del catalogo oficial.
CREATE VIEW v_contenido_propio AS
SELECT p.id AS paquete_id, p.clave_paquete, p.ruta_paquete
FROM m04_paquete_instalado p
WHERE p.origen = 'escuela' AND p.estado = 'activo';

-- Uso en modo repaso. No alimenta calificacion ni dominio.
CREATE VIEW v_repaso_uso AS
SELECT c.elemento_ref, count(*) AS aperturas, sum(c.segundos) AS segundos_total,
       count(DISTINCT s.persona_id) AS personas
FROM m08_repaso_consumo c
JOIN m08_repaso_sesion s ON s.id = c.repaso_sesion_id
GROUP BY c.elemento_ref;
