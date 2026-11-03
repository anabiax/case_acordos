-- =============================================================================
-- Consultas SQL relevantes (PostgreSQL - Modelo com Surrogate Keys / UUID)
-- Hierarquia: autor 1—N acordo 1—N documento
-- Uso: docker compose exec -T postgres psql -U postgres -d acordos < db/02_consultas.sql
-- =============================================================================


-- -----------------------------------------------------------------------------
-- 1) Quantidade de acordos e documentos por autor
--    Objetivo: apresentar o volume de acordos e documentos associados a cada autor.
--    Observação: utiliza LEFT JOIN para incluir autores sem acordos e COUNT(DISTINCT)
--    para evitar a duplicidade na contagem de acordos.
-- -----------------------------------------------------------------------------
docker compose exec postgres psql -U postgres -d acordos -c "
SELECT  au.id_externo              AS autor_id_externo,
        au.id                      AS autor_id_uuid,
        au.nome                    AS autor,
        COUNT(DISTINCT a.id)       AS qtd_acordos,
        COUNT(d.id)                AS qtd_documentos
FROM    autor au
LEFT JOIN acordo    a ON a.autor_id  = au.id
LEFT JOIN documento d ON d.acordo_id = a.id
GROUP BY au.id_externo, au.id, au.nome
ORDER BY au.id_externo;"


-- -----------------------------------------------------------------------------
-- 2) Ranking de autores por quantidade de acordos
--    Objetivo: identificar os autores com maior volume de acordos e documentos.
--    Observação: utiliza RANK() para classificar os autores e LIMIT para definir
--    a quantidade de registros apresentados.
-- -----------------------------------------------------------------------------
docker compose exec postgres psql -U postgres -d acordos -c "
SELECT  DENSE_RANK() OVER (
            ORDER BY
                COUNT(DISTINCT a.id) DESC,
                COUNT(d.id) DESC
        ) AS posicao,
        au.id_externo              AS autor_id_externo,
        au.id                      AS autor_id_uuid,
        au.nome                    AS autor,
        COUNT(DISTINCT a.id)       AS qtd_acordos,
        COUNT(d.id)                AS qtd_documentos
FROM    autor au
JOIN    acordo a             ON a.autor_id  = au.id
LEFT JOIN documento d        ON d.acordo_id = a.id
GROUP BY au.id_externo, au.id, au.nome
ORDER BY qtd_acordos DESC, qtd_documentos DESC, au.id_externo
LIMIT 5;"


-- -----------------------------------------------------------------------------
-- 3) Identificação de acordos sem documentos
--    Objetivo: localizar acordos que não possuem documentos associados.
--    Observação: utiliza NOT EXISTS para verificar a ausência de documentos
--    vinculados a cada acordo.
-- -----------------------------------------------------------------------------

docker compose exec postgres psql -U postgres -d acordos -c "
SELECT  a.id_externo AS acordo_id_externo,
        a.id         AS acordo_id_uuid,
        a.titulo     AS acordo,
        au.id_externo AS autor_id_externo,
        au.nome      AS autor
FROM    acordo a
JOIN    autor  au ON au.id = a.autor_id
WHERE   NOT EXISTS (SELECT 1 FROM documento d WHERE d.acordo_id = a.id)
ORDER BY au.id_externo, a.id_externo;"


-- -----------------------------------------------------------------------------
-- 4) Média de documentos por acordo, por autor
--    Objetivo: analisar a distribuição de documentos associados aos acordos
--    de cada autor.
--    Observação: utiliza NULLIF para evitar divisão por zero no cálculo da média.
-- -----------------------------------------------------------------------------
docker compose exec postgres psql -U postgres -d acordos -c "
SELECT  au.id_externo                                    AS autor_id_externo,
        au.id                                            AS autor_id_uuid,
        au.nome                                          AS autor,
        COUNT(DISTINCT a.id)                             AS qtd_acordos,
        COUNT(d.id)                                      AS qtd_documentos,
        ROUND(COUNT(d.id)::numeric
              / NULLIF(COUNT(DISTINCT a.id), 0), 1)      AS media_docs_por_acordo
FROM    autor au
LEFT JOIN acordo    a ON a.autor_id  = au.id
LEFT JOIN documento d ON d.acordo_id = a.id
GROUP BY au.id_externo, au.id, au.nome
ORDER BY media_docs_por_acordo DESC NULLS LAST, au.id_externo;"

-- -----------------------------------------------------------------------------
-- 5) Análise da completude dos dados cadastrais dos autores
--    Objetivo: identificar a quantidade de autores com informações cadastrais
--    ausentes ou não preenchidas.
--    Observação: considera os campos de telefone, cidade, empresa e segmento.
-- -----------------------------------------------------------------------------

docker compose exec postgres psql -U postgres -d acordos -c "SELECT
    COUNT(*) AS total_autores,
    COUNT(*) FILTER (
        WHERE telefone IS NULL OR TRIM(telefone) = ''
    ) AS sem_telefone,
    COUNT(*) FILTER (
        WHERE endereco_cidade IS NULL OR TRIM(endereco_cidade) = ''
    ) AS sem_cidade,
    COUNT(*) FILTER (
        WHERE empresa_nome IS NULL OR TRIM(empresa_nome) = ''
    ) AS sem_empresa,
    COUNT(*) FILTER (
        WHERE empresa_segmento IS NULL OR TRIM(empresa_segmento) = ''
    ) AS sem_segmento
FROM autor;"

-- -----------------------------------------------------------------------------
-- 6) Distribuição de autores por cidade
--    Objetivo: identificar a concentração de autores cadastrados por município.
--    Observação: apresenta também a quantidade de empresas distintas associadas
--    a cada cidade.
-- -----------------------------------------------------------------------------

docker compose exec postgres psql -U postgres -d acordos -c "SELECT
    endereco_cidade AS cidade,
    COUNT(*) AS qtd_autores,
    COUNT(DISTINCT empresa_nome) AS qtd_empresas
FROM autor
WHERE endereco_cidade IS NOT NULL
GROUP BY endereco_cidade
ORDER BY qtd_autores DESC, cidade;"

-- -----------------------------------------------------------------------------
-- 7) Verificação da quantidade de registros após a ingestão
--    Objetivo: conferir o volume total de autores, acordos e documentos
--    armazenados no banco de dados.
--    Observação: permite validar a carga inicial e verificar a idempotência
--    após execuções subsequentes do processo de ingestão.
-- -----------------------------------------------------------------------------
docker compose exec postgres psql -U postgres -d acordos -c "SELECT (SELECT COUNT(*) FROM autor) AS autores, (SELECT COUNT(*) FROM acordo) AS acordos, (SELECT COUNT(*) FROM documento) documentos;"
-- -----------------------------------------------------------------------------
-- (Esperado: 10 autores, 100 acordos, 5000 documentos. Rode a ingestão novamente para provar a idempotência sem duplicações).
-- -----------------------------------------------------------------------------


-- -----------------------------------------------------------------------------
-- 8) Inspeção dos identificadores internos e externos dos autores
--    Objetivo: visualizar os UUIDs gerados pelo banco de dados em comparação
--    com os identificadores originais disponibilizados pela API.
--    Observação: limita a exibição a cinco registros para facilitar a inspeção.
-- -----------------------------------------------------------------------------
docker compose exec postgres psql -U postgres -d acordos -c "SELECT id AS uuid_interno, id_externo, nome FROM autor LIMIT 5;"
