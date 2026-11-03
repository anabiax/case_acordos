using CaseAcordos.Api.Interfaces;
using CaseAcordos.Api.Modelo;
using CaseAcordos.Api.Tratamento;
using Dapper;
using Npgsql;

namespace CaseAcordos.Api.Repositorios
{

    /// <summary>
    /// Consultas de leitura em PostgreSQL via Dapper (sempre parametrizadas).
    ///
    /// ATENÇÃO: o contrato com o banco está concentrado nas constantes SQL abaixo.
    /// Tabelas (schema do projeto, db/01_schema.sql): autor, acordo, documento.
    /// Se o schema mudar, ajuste SOMENTE este bloco.
    /// </summary>
    public sealed class RepositorioConsultaPostgres(NpgsqlDataSource fonteDados) : IRepositorioConsulta
    {
        private const string SqlAutor = """
        SELECT a.id               AS Id,
               a.nome             AS Nome,
               a.email            AS Email,
               a.telefone         AS Telefone,
               a.endereco_rua         AS Rua,
               a.endereco_complemento AS Complemento,
               a.endereco_cidade      AS Cidade,
               a.endereco_cep         AS Cep,
               a.empresa_nome     AS EmpresaNome,
               a.empresa_segmento AS EmpresaSegmento
          FROM autor a
         WHERE a.id = @AutorId
        """;

        private const string SqlAcordosDoAutor = """
        SELECT ac.id                      AS Id,
               ac.titulo                  AS Titulo,
               COUNT(d.id)::int           AS QuantidadeDocumentos
          FROM acordo ac
          LEFT JOIN documento d ON d.acordo_id = ac.id
         WHERE ac.autor_id = @AutorId
         GROUP BY ac.id, ac.titulo
         ORDER BY ac.id
        """;

        // Uma página por acordo em UMA consulta: numera os documentos dentro de cada acordo
        // (ROW_NUMBER particionado) e filtra a janela [deslocamento+1 .. deslocamento+limite].
        private const string SqlDocumentosPaginadosPorAutor = """
        SELECT x.id            AS Id,
               x.acordo_id     AS AcordoId,
               x.titulo        AS Titulo,
               x.url           AS Url,
               x.thumbnail_url AS ThumbnailUrl
          FROM (
                SELECT d.id, d.acordo_id, d.titulo, d.url, d.thumbnail_url,
                       ROW_NUMBER() OVER (PARTITION BY d.acordo_id ORDER BY d.id) AS posicao
                  FROM documento d
                  JOIN acordo ac ON ac.id = d.acordo_id
                 WHERE ac.autor_id = @AutorId
               ) x
         WHERE x.posicao >  @Deslocamento
           AND x.posicao <= @Deslocamento + @Limite
         ORDER BY x.acordo_id, x.id
        """;

        private const string SqlAcordoDoAutor = """
        SELECT ac.id    AS Id,
               ac.titulo AS Titulo,
               (SELECT COUNT(*) FROM documento d WHERE d.acordo_id = ac.id)::int AS QuantidadeDocumentos
          FROM acordo ac
         WHERE ac.id = @AcordoId
           AND ac.autor_id = @AutorId
        """;

        private const string SqlDocumentosDoAcordo = """
        SELECT d.id            AS Id,
               d.acordo_id     AS AcordoId,
               d.titulo        AS Titulo,
               d.url           AS Url,
               d.thumbnail_url AS ThumbnailUrl
          FROM documento d
         WHERE d.acordo_id = @AcordoId
         ORDER BY d.id
        OFFSET @Deslocamento
         LIMIT @Limite
        """;

        public Task<AutorRegistro?> ObterAutorAsync(int autorId, CancellationToken cancellationToken)
            => ExecutarAsync(
                conexao => conexao.QuerySingleOrDefaultAsync<AutorRegistro>(
                    new CommandDefinition(SqlAutor, new { AutorId = autorId }, cancellationToken: cancellationToken)),
                cancellationToken);


        public Task<IReadOnlyList<AcordoRegistro>> ListarAcordosAsync(int autorId, CancellationToken cancellationToken)
            => ExecutarAsync<IReadOnlyList<AcordoRegistro>>(
                async conexao => (await conexao.QueryAsync<AcordoRegistro>(
                    new CommandDefinition(SqlAcordosDoAutor, new { AutorId = autorId }, cancellationToken: cancellationToken))).ToList(),
                cancellationToken);


        public Task<IReadOnlyList<DocumentoRegistro>> ListarDocumentosPaginadosPorAutorAsync(
            int autorId, long deslocamento, int limite, CancellationToken cancellationToken)
            => ExecutarAsync<IReadOnlyList<DocumentoRegistro>>(
                async conexao => (await conexao.QueryAsync<DocumentoRegistro>(
                    new CommandDefinition(
                        SqlDocumentosPaginadosPorAutor,
                        new { AutorId = autorId, Deslocamento = deslocamento, Limite = limite },
                        cancellationToken: cancellationToken))).ToList(),
                cancellationToken);


        public Task<AcordoRegistro?> ObterAcordoDoAutorAsync(int autorId, int acordoId, CancellationToken cancellationToken)
            => ExecutarAsync(
                conexao => conexao.QuerySingleOrDefaultAsync<AcordoRegistro>(
                    new CommandDefinition(
                        SqlAcordoDoAutor,
                        new { AutorId = autorId, AcordoId = acordoId },
                        cancellationToken: cancellationToken)),
                cancellationToken);


        public Task<IReadOnlyList<DocumentoRegistro>> ListarDocumentosDoAcordoAsync(
            int acordoId, long deslocamento, int limite, CancellationToken cancellationToken)
            => ExecutarAsync<IReadOnlyList<DocumentoRegistro>>(
                async conexao => (await conexao.QueryAsync<DocumentoRegistro>(
                    new CommandDefinition(
                        SqlDocumentosDoAcordo,
                        new { AcordoId = acordoId, Deslocamento = deslocamento, Limite = limite },
                        cancellationToken: cancellationToken))).ToList(),
                cancellationToken);


        /// <summary>Abre a conexão (do pool), executa a consulta e traduz falhas do driver em <see cref="FalhaConsultaException"/>.</summary>
        private async Task<T> ExecutarAsync<T>(Func<NpgsqlConnection, Task<T>> consulta, CancellationToken cancellationToken)
        {
            try
            {
                await using var conexao = await fonteDados.OpenConnectionAsync(cancellationToken);
                return await consulta(conexao);
            }
            catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
            {
                throw new FalhaConsultaException("Falha ao consultar a base de dados.", ex);
            }
        }
    }
}