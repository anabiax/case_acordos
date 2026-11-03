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
        // A API recebe os IDs externos da origem jsonplaceholder.
        // Os IDs internos das tabelas são UUID e não devem ser comparados com int.
        private const short OrigemDadosId = 1;

        private const string SqlAutor = """
        SELECT a.id_externo       AS Id,
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
         WHERE a.id_externo = @AutorId
           AND a.origem_id = @OrigemId
        """;

        private const string SqlAcordosDoAutor = """
        SELECT ac.id_externo              AS IdExterno,
               ac.titulo                  AS Titulo,
               COUNT(d.id)::int           AS QuantidadeDocumentos
          FROM acordo ac
          JOIN autor a ON a.id = ac.autor_id
          LEFT JOIN documento d ON d.acordo_id = ac.id
         WHERE a.id_externo = @AutorId
           AND a.origem_id = @OrigemId
         GROUP BY ac.id_externo, ac.titulo
         ORDER BY ac.id_externo
        """;

        // Uma página por acordo em UMA consulta: numera os documentos dentro de cada acordo
        // (ROW_NUMBER particionado) e filtra a janela [deslocamento+1 .. deslocamento+limite].
        private const string SqlDocumentosPaginadosPorAutor = """
        SELECT x.id_externo          AS IdExterno,
               x.acordo_id_externo   AS AcordoIdExterno,
               x.titulo        AS Titulo,
               x.url           AS Url,
               x.thumbnail_url AS ThumbnailUrl
          FROM (
                SELECT d.id,
                       d.id_externo,
                       ac.id_externo AS acordo_id_externo,
                       d.acordo_id,
                       d.titulo,
                       d.url,
                       d.thumbnail_url,
                       ROW_NUMBER() OVER (PARTITION BY d.acordo_id ORDER BY d.id) AS posicao
                  FROM documento d
                  JOIN acordo ac ON ac.id = d.acordo_id
                 JOIN autor a ON a.id = ac.autor_id
                 WHERE a.id_externo = @AutorId
                   AND a.origem_id = @OrigemId
               ) x
         WHERE x.posicao >  @Deslocamento
           AND x.posicao <= @Deslocamento + @Limite
         ORDER BY x.acordo_id, x.id
        """;

        private const string SqlAcordoDoAutor = """
        SELECT ac.id_externo AS IdExterno,
               ac.titulo AS Titulo,
               (SELECT COUNT(*) FROM documento d WHERE d.acordo_id = ac.id)::int AS QuantidadeDocumentos
          FROM acordo ac
          JOIN autor a ON a.id = ac.autor_id
         WHERE ac.id_externo = @AcordoId
           AND a.id_externo = @AutorId
           AND a.origem_id = @OrigemId
        """;

        private const string SqlDocumentosDoAcordo = """
        SELECT d.id_externo    AS IdExterno,
               ac.id_externo   AS AcordoIdExterno,
               d.titulo        AS Titulo,
               d.url           AS Url,
               d.thumbnail_url AS ThumbnailUrl
          FROM documento d
          JOIN acordo ac ON ac.id = d.acordo_id
          JOIN autor a ON a.id = ac.autor_id
         WHERE ac.id_externo = @AcordoId
           AND a.origem_id = @OrigemId
         ORDER BY d.id
        OFFSET @Deslocamento
         LIMIT @Limite
        """;

        public Task<AutorRegistro?> ObterAutorPorIdExternoAsync(int autorId, CancellationToken cancellationToken)
            => ExecutarAsync(
                conexao => conexao.QuerySingleOrDefaultAsync<AutorRegistro>(
                    new CommandDefinition(
                        SqlAutor,
                        new { AutorId = autorId, OrigemId = OrigemDadosId },
                        cancellationToken: cancellationToken)),
                cancellationToken);


        public Task<IReadOnlyList<AcordoRegistro>> ListarAcordosPorAutorExternoAsync(int autorId, CancellationToken cancellationToken)
            => ExecutarAsync<IReadOnlyList<AcordoRegistro>>(
                async conexao => (await conexao.QueryAsync<AcordoRegistro>(
                    new CommandDefinition(
                        SqlAcordosDoAutor,
                        new { AutorId = autorId, OrigemId = OrigemDadosId },
                        cancellationToken: cancellationToken))).ToList(),
                cancellationToken);


        public Task<IReadOnlyList<DocumentoRegistro>> ListarDocumentosPaginadosPorAutorExternoAsync(
            int autorId, long deslocamento, int limite, CancellationToken cancellationToken)
            => ExecutarAsync<IReadOnlyList<DocumentoRegistro>>(
                async conexao => (await conexao.QueryAsync<DocumentoRegistro>(
                    new CommandDefinition(
                        SqlDocumentosPaginadosPorAutor,
                        new
                        {
                            AutorId = autorId,
                            OrigemId = OrigemDadosId,
                            Deslocamento = deslocamento,
                            Limite = limite
                        },
                        cancellationToken: cancellationToken))).ToList(),
                cancellationToken);


        public Task<AcordoRegistro?> ObterAcordoDoAutorPorIdExternoAsync(int autorId, int acordoId, CancellationToken cancellationToken)
            => ExecutarAsync(
                conexao => conexao.QuerySingleOrDefaultAsync<AcordoRegistro>(
                    new CommandDefinition(
                        SqlAcordoDoAutor,
                        new
                        {
                            AutorId = autorId,
                            AcordoId = acordoId,
                            OrigemId = OrigemDadosId
                        },
                        cancellationToken: cancellationToken)),
                cancellationToken);


        public Task<IReadOnlyList<DocumentoRegistro>> ListarDocumentosDoAcordoPorIdExternoAsync(
            int acordoId, long deslocamento, int limite, CancellationToken cancellationToken)
            => ExecutarAsync<IReadOnlyList<DocumentoRegistro>>(
                async conexao => (await conexao.QueryAsync<DocumentoRegistro>(
                    new CommandDefinition(
                        SqlDocumentosDoAcordo,
                        new
                        {
                            AcordoId = acordoId,
                            OrigemId = OrigemDadosId,
                            Deslocamento = deslocamento,
                            Limite = limite
                        },
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