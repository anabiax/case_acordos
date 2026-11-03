-- =============================================================================
-- Consultas SQL relevantes (PostgreSQL)
-- Hierarquia: autor 1—N acordo 1—N documento
-- Uso: docker compose exec -T postgres psql -U postgres -d acordos < db/02_consultas.sql
-- =============================================================================


-- -----------------------------------------------------------------------------
-- 1) Quantidade de acordos e de documentos por autor
--    (é exatamente o "Resumo" devolvido por GET /api/v1/usuarios/{id}/acordos)
--    Dois LEFT JOIN em cascata multiplicam linhas: por isso COUNT(DISTINCT a.id)
--    para acordos e COUNT(d.id) para documentos. Autor sem acordos aparece com 0.
-- -----------------------------------------------------------------------------
SELECT  au.id                      AS autor_id,
        au.nome                    AS autor,
        COUNT(DISTINCT a.id)       AS qtd_acordos,
        COUNT(d.id)                AS qtd_documentos
FROM    autor au
LEFT JOIN acordo    a ON a.autor_id  = au.id
LEFT JOIN documento d ON d.acordo_id = a.id
GROUP BY au.id, au.nome
ORDER BY au.id;


-- -----------------------------------------------------------------------------
-- 2) Ranking: autores com maior volume de acordos (e de documentos)
--    Desempate por documentos e depois por id, para o resultado ser determinístico.
--    Troque o LIMIT para mudar o tamanho do ranking.
-- -----------------------------------------------------------------------------
SELECT  RANK() OVER (ORDER BY COUNT(DISTINCT a.id) DESC) AS posicao,
        au.id                      AS autor_id,
        au.nome                    AS autor,
        COUNT(DISTINCT a.id)       AS qtd_acordos,
        COUNT(d.id)                AS qtd_documentos
FROM    autor au
JOIN    acordo a             ON a.autor_id  = au.id
LEFT JOIN documento d        ON d.acordo_id = a.id
GROUP BY au.id, au.nome
ORDER BY qtd_acordos DESC, qtd_documentos DESC, au.id
LIMIT 5;


-- -----------------------------------------------------------------------------
-- 3) Acordos sem documentos (possível falha de ingestão ou dado incompleto na origem)
--    NOT EXISTS é o formato mais barato: para na primeira linha encontrada
--    e usa o índice de documento(acordo_id).
-- -----------------------------------------------------------------------------
SELECT  a.id        AS acordo_id,
        a.titulo    AS acordo,
        au.id       AS autor_id,
        au.nome     AS autor
FROM    acordo a
JOIN    autor  au ON au.id = a.autor_id
WHERE   NOT EXISTS (SELECT 1 FROM documento d WHERE d.acordo_id = a.id)
ORDER BY au.id, a.id;


-- -----------------------------------------------------------------------------
-- 4) Média de documentos por acordo, por autor (distribuição da carga)
--    Útil para a demonstração: após a ingestão, todos os autores devem ter o mesmo perfil.
--    NULLIF evita divisão por zero para autor sem acordos.
-- -----------------------------------------------------------------------------
SELECT  au.id                                            AS autor_id,
        au.nome                                          AS autor,
        COUNT(DISTINCT a.id)                             AS qtd_acordos,
        COUNT(d.id)                                      AS qtd_documentos,
        ROUND(COUNT(d.id)::numeric
              / NULLIF(COUNT(DISTINCT a.id), 0), 1)      AS media_docs_por_acordo
FROM    autor au
LEFT JOIN acordo    a ON a.autor_id  = au.id
LEFT JOIN documento d ON d.acordo_id = a.id
GROUP BY au.id, au.nome
ORDER BY media_docs_por_acordo DESC NULLS LAST, au.id;
