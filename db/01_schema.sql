-- Case prático Acordos - PostgreSQL
-- Hierarquia: autor (1) -> (N) acordo (1) -> (N) documento
-- PKs usam os ids da API pública (sem identity) para permitir upsert idempotente.
-- Quantidades (acordos/documentos) são derivadas via COUNT; não são persistidas.

CREATE TABLE IF NOT EXISTS autor (
    id                  INTEGER       NOT NULL,
    nome                VARCHAR(150)  NOT NULL,
    email               VARCHAR(200)  NOT NULL,
    telefone            VARCHAR(50),
    endereco_rua        VARCHAR(200),   -- address.street
    endereco_complemento VARCHAR(100),  -- address.suite
    endereco_cidade     VARCHAR(100),   -- address.city
    endereco_cep        VARCHAR(20),    -- address.zipcode
    empresa_nome        VARCHAR(200),   -- company.name
    empresa_segmento    VARCHAR(300),   -- company.bs
    CONSTRAINT pk_autor PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS acordo (
    id        INTEGER      NOT NULL,
    autor_id  INTEGER      NOT NULL,
    titulo    VARCHAR(500) NOT NULL,
    CONSTRAINT pk_acordo PRIMARY KEY (id),
    CONSTRAINT fk_acordo_autor FOREIGN KEY (autor_id) REFERENCES autor (id)
);

CREATE TABLE IF NOT EXISTS documento (
    id            INTEGER       NOT NULL,
    acordo_id     INTEGER       NOT NULL,
    titulo        VARCHAR(500)  NOT NULL,
    url           VARCHAR(1000) NOT NULL,
    thumbnail_url VARCHAR(1000) NOT NULL,
    CONSTRAINT pk_documento PRIMARY KEY (id),
    CONSTRAINT fk_documento_acordo FOREIGN KEY (acordo_id) REFERENCES acordo (id)
);

-- Índices nas FKs (Postgres não os cria automaticamente): sustentam a consulta
-- por autor -> acordos -> documentos e os COUNT do resumo.
CREATE INDEX IF NOT EXISTS ix_acordo_autor_id     ON acordo (autor_id);
CREATE INDEX IF NOT EXISTS ix_documento_acordo_id ON documento (acordo_id);
