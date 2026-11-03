-- Tabela de metadados das origens de dados
CREATE TABLE IF NOT EXISTS origem_dados (
    id    SMALLINT PRIMARY KEY,
    nome  VARCHAR(50) NOT NULL UNIQUE
);

-- Inserindo a origem padrão (ex: jsonplaceholder)
INSERT INTO origem_dados (id, nome) VALUES (1, 'jsonplaceholder') ON CONFLICT DO NOTHING;

-- Tabela Autor com UUID e chave alternativa (origem_id + id_externo)
CREATE TABLE IF NOT EXISTS autor (
    id                   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    origem_id            SMALLINT     NOT NULL REFERENCES origem_dados(id),
    id_externo           INTEGER      NOT NULL,
    nome                 VARCHAR(150) NOT NULL,
    email                VARCHAR(200) NOT NULL,
    telefone             VARCHAR(50),
    endereco_rua         VARCHAR(200),
    endereco_complemento VARCHAR(100),
    endereco_cidade      VARCHAR(100),
    endereco_cep         VARCHAR(20),
    empresa_nome         VARCHAR(200),
    empresa_segmento     VARCHAR(300),
    criado_em            TIMESTAMPTZ NOT NULL DEFAULT now(),
    atualizado_em        TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_autor_origem UNIQUE (origem_id, id_externo)
);

-- Tabela Acordo vinculada ao UUID do Autor
CREATE TABLE IF NOT EXISTS acordo (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    autor_id      UUID    NOT NULL REFERENCES autor(id) ON DELETE CASCADE,
    id_externo    INTEGER NOT NULL,
    titulo        VARCHAR(500) NOT NULL,
    criado_em     TIMESTAMPTZ NOT NULL DEFAULT now(),
    atualizado_em TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_acordo_externo UNIQUE (autor_id, id_externo)
);

CREATE INDEX IF NOT EXISTS ix_acordo_autor ON acordo(autor_id);

-- Tabela Documento vinculada ao UUID do Acordo
CREATE TABLE IF NOT EXISTS documento (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    acordo_id     UUID    NOT NULL REFERENCES acordo(id) ON DELETE CASCADE,
    id_externo    INTEGER NOT NULL,
    titulo        VARCHAR(500)  NOT NULL,
    url           VARCHAR(1000) NOT NULL,
    thumbnail_url VARCHAR(1000) NOT NULL,
    criado_em     TIMESTAMPTZ NOT NULL DEFAULT now(),
    atualizado_em TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_documento_externo UNIQUE (acordo_id, id_externo)
);

CREATE INDEX IF NOT EXISTS ix_documento_acordo ON documento(acordo_id);