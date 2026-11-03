using CaseAcordos.Contratos.Mensagens;
using CaseAcordos.Persistencia.Excecoes;
using CaseAcordos.Persistencia.Interfaces;
using Dapper;
using Npgsql;

namespace CaseAcordos.Persistencia.Repositorios;

/// <summary>
/// Persiste mensagens de acordos no PostgreSQL.
/// Os IDs recebidos da mensagem são IDs externos inteiros; os IDs UUID internos
/// são obtidos do banco e usados apenas nos relacionamentos entre as tabelas.
/// </summary>
public sealed class RepositorioAcordosPostgres(NpgsqlDataSource fonteDados)
    : IRepositorioAcordos
{
    private const short OrigemDadosId = 1;

    private const string SqlAutor = """
        INSERT INTO autor (
            origem_id,
            id_externo,
            nome,
            email,
            telefone,
            endereco_rua,
            endereco_complemento,
            endereco_cidade,
            endereco_cep,
            empresa_nome,
            empresa_segmento,
            atualizado_em)
        VALUES (
            @OrigemId,
            @IdExterno,
            @Nome,
            @Email,
            @Telefone,
            @EnderecoRua,
            @EnderecoComplemento,
            @EnderecoCidade,
            @EnderecoCep,
            @EmpresaNome,
            @EmpresaSegmento,
            now())
        ON CONFLICT (origem_id, id_externo)
        DO UPDATE SET
            nome = EXCLUDED.nome,
            email = EXCLUDED.email,
            telefone = EXCLUDED.telefone,
            endereco_rua = EXCLUDED.endereco_rua,
            endereco_complemento = EXCLUDED.endereco_complemento,
            endereco_cidade = EXCLUDED.endereco_cidade,
            endereco_cep = EXCLUDED.endereco_cep,
            empresa_nome = EXCLUDED.empresa_nome,
            empresa_segmento = EXCLUDED.empresa_segmento,
            atualizado_em = now()
        RETURNING id;
        """;

    private const string SqlAcordo = """
        INSERT INTO acordo (
            autor_id,
            id_externo,
            titulo,
            atualizado_em)
        VALUES (
            @AutorId,
            @IdExterno,
            @Titulo,
            now())
        ON CONFLICT (autor_id, id_externo)
        DO UPDATE SET
            titulo = EXCLUDED.titulo,
            atualizado_em = now()
        RETURNING id;
        """;

    private const string SqlDocumento = """
        INSERT INTO documento (
            acordo_id,
            id_externo,
            titulo,
            url,
            thumbnail_url,
            atualizado_em)
        VALUES (
            @AcordoId,
            @IdExterno,
            @Titulo,
            @Url,
            @ThumbnailUrl,
            now())
        ON CONFLICT (acordo_id, id_externo)
        DO UPDATE SET
            titulo = EXCLUDED.titulo,
            url = EXCLUDED.url,
            thumbnail_url = EXCLUDED.thumbnail_url,
            atualizado_em = now();
        """;

    public async Task GravarAsync(MensagemAcordo mensagem, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(mensagem);

        await using var conexao = await fonteDados.OpenConnectionAsync(ct);
        await using var transacao = await conexao.BeginTransactionAsync(ct);

        try
        {
            var autorId = await conexao.ExecuteScalarAsync<Guid>(
                new CommandDefinition(
                    SqlAutor,
                    new
                    {
                        OrigemId = OrigemDadosId,
                        IdExterno = mensagem.Autor.Id,
                        mensagem.Autor.Nome,
                        mensagem.Autor.Email,
                        Telefone = mensagem.Autor.Telefone,
                        EnderecoRua = mensagem.Autor.EnderecoRua,
                        EnderecoComplemento = mensagem.Autor.EnderecoComplemento,
                        EnderecoCidade = mensagem.Autor.EnderecoCidade,
                        EnderecoCep = mensagem.Autor.EnderecoCep,
                        EmpresaNome = mensagem.Autor.EmpresaNome,
                        EmpresaSegmento = mensagem.Autor.EmpresaSegmento
                    },
                    transaction: transacao,
                    cancellationToken: ct));

            var acordoId = await conexao.ExecuteScalarAsync<Guid>(
                new CommandDefinition(
                    SqlAcordo,
                    new
                    {
                        AutorId = autorId,
                        IdExterno = mensagem.Acordo.Id,
                        mensagem.Acordo.Titulo
                    },
                    transaction: transacao,
                    cancellationToken: ct));

            foreach (var documento in mensagem.Documentos)
            {
                await conexao.ExecuteAsync(
                    new CommandDefinition(
                        SqlDocumento,
                        new
                        {
                            AcordoId = acordoId,
                            IdExterno = documento.Id,
                            documento.Titulo,
                            documento.Url,
                            documento.ThumbnailUrl
                        },
                        transaction: transacao,
                        cancellationToken: ct));
            }

            await transacao.CommitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PostgresException ex)
        {
            await TentarRollbackAsync(transacao);
            throw new FalhaPersistenciaException(
                "Falha ao gravar o acordo na base de dados.",
                EhRetentavel(ex),
                ex);
        }
        catch (NpgsqlException ex)
        {
            await TentarRollbackAsync(transacao);
            throw new FalhaPersistenciaException(
                "Falha ao gravar o acordo na base de dados.",
                retentavel: true,
                ex);
        }
        catch (TimeoutException ex)
        {
            await TentarRollbackAsync(transacao);
            throw new FalhaPersistenciaException(
                "Tempo excedido ao gravar o acordo na base de dados.",
                retentavel: true,
                ex);
        }
        catch (Exception ex)
        {
            await TentarRollbackAsync(transacao);
            throw new FalhaPersistenciaException(
                "Falha ao gravar o acordo na base de dados.",
                retentavel: false,
                ex);
        }
    }

    private static bool EhRetentavel(PostgresException ex)
        => ex.SqlState is "40001"    // serialization_failure
            or "40P01"                // deadlock_detected
            or "53300"                // too_many_connections
            or "57P01"                // admin_shutdown
            or "08000"                // connection_exception
            or "08003"                // connection_does_not_exist
            or "08006";               // connection_failure

    private static async Task TentarRollbackAsync(NpgsqlTransaction transacao)
    {
        try
        {
            await transacao.RollbackAsync();
        }
        catch
        {
            // A exceção original é mais importante que uma falha no rollback.
        }
    }
}