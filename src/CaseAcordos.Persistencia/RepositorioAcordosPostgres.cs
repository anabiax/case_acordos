using CaseAcordos.Contratos;
using Dapper;
using Npgsql;

namespace CaseAcordos.Persistencia;

/// <summary>
/// Persistência em PostgreSQL com Npgsql + Dapper.
/// Idempotência: INSERT ... ON CONFLICT (id) DO UPDATE ... WHERE (valores atuais) IS DISTINCT FROM (novos).
/// O UPDATE só acontece quando algum valor mudou, o que evita escrita e contenção de lock desnecessárias
/// quando várias mensagens do mesmo autor chegam em paralelo.
/// </summary>
public sealed class RepositorioAcordosPostgres : IRepositorioAcordos
{
    private const string SqlAutor = """
        INSERT INTO autor (id, nome, email, telefone, endereco_rua, endereco_complemento, endereco_cidade, endereco_cep, empresa_nome, empresa_segmento)
        VALUES (@Id, @Nome, @Email, @Telefone, @EnderecoRua, @EnderecoComplemento, @EnderecoCidade, @EnderecoCep, @EmpresaNome, @EmpresaSegmento)
        ON CONFLICT (id) DO UPDATE SET
            nome = EXCLUDED.nome,
            email = EXCLUDED.email,
            telefone = EXCLUDED.telefone,
            endereco_rua = EXCLUDED.endereco_rua,
            endereco_complemento = EXCLUDED.endereco_complemento,
            endereco_cidade = EXCLUDED.endereco_cidade,
            endereco_cep = EXCLUDED.endereco_cep,
            empresa_nome = EXCLUDED.empresa_nome,
            empresa_segmento = EXCLUDED.empresa_segmento
        WHERE (autor.nome, autor.email, autor.telefone, autor.endereco_rua, autor.endereco_complemento,
               autor.endereco_cidade, autor.endereco_cep, autor.empresa_nome, autor.empresa_segmento)
              IS DISTINCT FROM
              (EXCLUDED.nome, EXCLUDED.email, EXCLUDED.telefone, EXCLUDED.endereco_rua, EXCLUDED.endereco_complemento,
               EXCLUDED.endereco_cidade, EXCLUDED.endereco_cep, EXCLUDED.empresa_nome, EXCLUDED.empresa_segmento);
        """;

    private const string SqlAcordo = """
        INSERT INTO acordo (id, autor_id, titulo)
        VALUES (@Id, @AutorId, @Titulo)
        ON CONFLICT (id) DO UPDATE SET
            autor_id = EXCLUDED.autor_id,
            titulo = EXCLUDED.titulo
        WHERE (acordo.autor_id, acordo.titulo) IS DISTINCT FROM (EXCLUDED.autor_id, EXCLUDED.titulo);
        """;

    private const string SqlDocumento = """
        INSERT INTO documento (id, acordo_id, titulo, url, thumbnail_url)
        VALUES (@Id, @AcordoId, @Titulo, @Url, @ThumbnailUrl)
        ON CONFLICT (id) DO UPDATE SET
            acordo_id = EXCLUDED.acordo_id,
            titulo = EXCLUDED.titulo,
            url = EXCLUDED.url,
            thumbnail_url = EXCLUDED.thumbnail_url
        WHERE (documento.acordo_id, documento.titulo, documento.url, documento.thumbnail_url)
              IS DISTINCT FROM
              (EXCLUDED.acordo_id, EXCLUDED.titulo, EXCLUDED.url, EXCLUDED.thumbnail_url);
        """;

    private readonly NpgsqlDataSource _fonteDados;

    public RepositorioAcordosPostgres(NpgsqlDataSource fonteDados)
    {
        _fonteDados = fonteDados;
    }

    public async Task GravarAsync(MensagemAcordo mensagem, CancellationToken ct)
    {
        try
        {
            await using var conexao = await _fonteDados.OpenConnectionAsync(ct);
            await using var transacao = await conexao.BeginTransactionAsync(ct);

            var autor = mensagem.Autor;
            await conexao.ExecuteAsync(new CommandDefinition(
                SqlAutor, parameters: autor, transaction: transacao, cancellationToken: ct));

            await conexao.ExecuteAsync(new CommandDefinition(
                SqlAcordo,
                parameters: new { mensagem.Acordo.Id, AutorId = autor.Id, mensagem.Acordo.Titulo },
                transaction: transacao,
                cancellationToken: ct));

            var documentos = mensagem.Documentos
                .Select(d => new { d.Id, AcordoId = mensagem.Acordo.Id, d.Titulo, d.Url, d.ThumbnailUrl })
                .ToList();

            if (documentos.Count > 0)
            {
                // Dapper executa o comando uma vez para cada item da lista, dentro da mesma transação.
                await conexao.ExecuteAsync(new CommandDefinition(
                    SqlDocumento, parameters: documentos, transaction: transacao, cancellationToken: ct));
            }

            await transacao.CommitAsync(ct);
        }
        catch (PostgresException ex) when (EhErroDeDados(ex))
        {
            // 22 = dado inválido (ex.: texto maior que a coluna), 23 = integridade, 42 = SQL/esquema.
            // Tentar de novo não resolve: a mensagem vai direto para a DLQ.
            throw new FalhaPersistenciaException(
                $"Dado rejeitado pelo banco (SQLSTATE {ex.SqlState}): {ex.MessageText}", retentavel: false, ex);
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException or IOException)
        {
            // Conexão, timeout, deadlock (40P01), serialização (40001), desligamento do servidor (57P01)...
            throw new FalhaPersistenciaException(
                $"Falha transitória ao gravar no banco: {ex.Message}", retentavel: true, ex);
        }
    }

    private static bool EhErroDeDados(PostgresException ex) =>
        ex.SqlState is { Length: >= 2 } estado &&
        (estado.StartsWith("22", StringComparison.Ordinal) ||
         estado.StartsWith("23", StringComparison.Ordinal) ||
         estado.StartsWith("42", StringComparison.Ordinal));
}
